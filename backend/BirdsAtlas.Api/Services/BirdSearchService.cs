using System.Globalization;
using System.Text.Json;
using BirdsAtlas.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsAtlas.Api.Services;

public sealed class BirdSearchService
{
    private const long AvesTaxonId = 3;
    private static readonly HashSet<string> Continents = new(StringComparer.OrdinalIgnoreCase)
    {
        "AFRICA", "ASIA", "EUROPE", "NORTH_AMERICA", "SOUTH_AMERICA", "OCEANIA", "ANTARCTICA"
    };

    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _enrichmentGate = new(8, 8);
    private readonly SemaphoreSlim _occurrenceGate = new(12, 12);

    public BirdSearchService(IHttpClientFactory clients, IMemoryCache cache)
    {
        _clients = clients;
        _cache = cache;
    }

    public async Task<BirdListResponse> SearchAsync(
        string? query,
        int page,
        int pageSize,
        string? continent,
        string? order,
        string? family,
        string? genus,
        string? sort,
        CancellationToken cancellationToken)
    {
        continent = NormalizeContinent(continent);
        var taxonomy = await ResolveTaxonomyAsync(order, family, genus, cancellationToken);
        if (!taxonomy.Valid)
            return new BirdListResponse(page, pageSize, 0, false, Array.Empty<BirdSummary>());

        var byName = string.Equals(sort, "name", StringComparison.OrdinalIgnoreCase);
        if (continent is null)
        {
            var source = byName
                ? await FetchNamePageAsync(query, page, pageSize, taxonomy.TaxonId, cancellationToken)
                : await FetchPageAsync(query, page, pageSize, taxonomy.TaxonId, cancellationToken);
            var items = await EnrichAsync(source.Taxa, cancellationToken);
            return new BirdListResponse(page, pageSize, source.Total, false, items);
        }

        return byName
            ? await SearchContinentByNameAsync(query, page, pageSize, continent, taxonomy.TaxonId, cancellationToken)
            : await SearchContinentByObservationsAsync(query, page, pageSize, continent, taxonomy.TaxonId, cancellationToken);
    }

    private async Task<BirdListResponse> SearchContinentByNameAsync(
        string? query,
        int page,
        int pageSize,
        string continent,
        long taxonId,
        CancellationToken cancellationToken)
    {
        var taxa = await FetchAllNameOrderedAsync(query, taxonId, cancellationToken);
        return await FilterOrderedSourceAsync(taxa, page, pageSize, continent, cancellationToken);
    }

    private async Task<BirdListResponse> SearchContinentByObservationsAsync(
        string? query,
        int page,
        int pageSize,
        string continent,
        long taxonId,
        CancellationToken cancellationToken)
    {
        const int sourcePageSize = 100;
        var offset = (page - 1) * pageSize;
        var needed = offset + pageSize + 1;
        var matches = new List<BirdSummary>(needed);
        long total = long.MaxValue;
        long processed = 0;
        var sourcePage = 1;

        while (matches.Count < needed && processed < total)
        {
            var source = await FetchPageAsync(query, sourcePage, sourcePageSize, taxonId, cancellationToken);
            total = source.Total;
            if (source.Taxa.Count == 0) break;
            processed += source.Taxa.Count;
            matches.AddRange(await FilterContinentAsync(source.Taxa, continent, cancellationToken));
            sourcePage++;
        }

        return BuildFilteredResponse(matches, processed >= total, page, pageSize);
    }

    private async Task<BirdListResponse> FilterOrderedSourceAsync(
        IReadOnlyList<SearchTaxon> taxa,
        int page,
        int pageSize,
        string continent,
        CancellationToken cancellationToken)
    {
        const int batchSize = 100;
        var offset = (page - 1) * pageSize;
        var needed = offset + pageSize + 1;
        var matches = new List<BirdSummary>(needed);
        var processed = 0;

        while (matches.Count < needed && processed < taxa.Count)
        {
            var batch = taxa.Skip(processed).Take(batchSize).ToList();
            processed += batch.Count;
            matches.AddRange(await FilterContinentAsync(batch, continent, cancellationToken));
        }

        return BuildFilteredResponse(matches, processed >= taxa.Count, page, pageSize);
    }

    private static BirdListResponse BuildFilteredResponse(
        IReadOnlyList<BirdSummary> matches,
        bool exhausted,
        int page,
        int pageSize)
    {
        var offset = (page - 1) * pageSize;
        var items = matches.Skip(offset).Take(pageSize).ToList();
        if (exhausted)
            return new BirdListResponse(page, pageSize, matches.Count, false, items);

        var estimate = Math.Max((long)offset + items.Count + 1, (long)page * pageSize + 1);
        return new BirdListResponse(page, pageSize, estimate, true, items);
    }

    private async Task<IReadOnlyList<BirdSummary>> FilterContinentAsync(
        IReadOnlyList<SearchTaxon> taxa,
        string continent,
        CancellationToken cancellationToken)
    {
        var birds = await EnrichAsync(taxa, cancellationToken);
        var checks = await Task.WhenAll(birds.Select(async bird =>
        {
            if (bird.GbifKey is null) return false;
            return await HasOccurrenceAsync(bird.GbifKey.Value, continent, cancellationToken);
        }));

        return birds.Where((_, index) => checks[index]).ToList();
    }

    private async Task<IReadOnlyList<BirdSummary>> EnrichAsync(
        IReadOnlyList<SearchTaxon> taxa,
        CancellationToken cancellationToken)
    {
        return await Task.WhenAll(taxa.Select(taxon => EnrichOneAsync(taxon, cancellationToken)));
    }

    private async Task<BirdSummary> EnrichOneAsync(SearchTaxon taxon, CancellationToken cancellationToken)
    {
        await _enrichmentGate.WaitAsync(cancellationToken);
        try
        {
            var gbif = await MatchGbifAsync(taxon.ScientificName, cancellationToken);
            return new BirdSummary(
                taxon.Id,
                taxon.CommonName,
                taxon.EnglishName,
                taxon.ScientificName,
                gbif.Family,
                gbif.Order,
                gbif.Genus,
                taxon.ImageUrl,
                taxon.PhotoAttribution,
                taxon.PhotoLicense,
                taxon.IucnStatus,
                taxon.ObservationCount,
                gbif.Key);
        }
        finally
        {
            _enrichmentGate.Release();
        }
    }

    private async Task<(long Total, IReadOnlyList<SearchTaxon> Taxa)> FetchPageAsync(
        string? query,
        int page,
        int perPage,
        long taxonId,
        CancellationToken cancellationToken)
    {
        var queryPart = string.IsNullOrWhiteSpace(query)
            ? string.Empty
            : $"&q={Uri.EscapeDataString(query.Trim())}";
        var path = $"v1/taxa?taxon_id={taxonId}&rank=species&is_active=true&all_names=true&locale=nl&per_page={perPage}&page={page}&order_by=observations_count&order=desc{queryPart}";
        return await FetchTaxaAsync(path, cancellationToken);
    }

    private async Task<(long Total, IReadOnlyList<SearchTaxon> Taxa)> FetchNamePageAsync(
        string? query,
        int page,
        int pageSize,
        long taxonId,
        CancellationToken cancellationToken)
    {
        var ordered = await FetchAllNameOrderedAsync(query, taxonId, cancellationToken);
        return (ordered.Count, ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList());
    }

    private async Task<IReadOnlyList<SearchTaxon>> FetchAllNameOrderedAsync(
        string? query,
        long taxonId,
        CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var key = $"bird-name-order:{taxonId}:{normalizedQuery.ToLowerInvariant()}";
        if (_cache.TryGetValue<IReadOnlyList<SearchTaxon>>(key, out var cached)) return cached;

        const int perPage = 500;
        var all = new List<SearchTaxon>();
        long? idAbove = null;
        while (true)
        {
            var queryPart = normalizedQuery.Length == 0
                ? string.Empty
                : $"&q={Uri.EscapeDataString(normalizedQuery)}";
            var cursorPart = idAbove is null ? string.Empty : $"&id_above={idAbove.Value}";
            var path = $"v1/taxa?taxon_id={taxonId}&rank=species&is_active=true&all_names=true&locale=nl&per_page={perPage}&page=1&order_by=id&order=asc{cursorPart}{queryPart}";
            var source = await FetchTaxaAsync(path, cancellationToken);
            if (source.Taxa.Count == 0) break;

            all.AddRange(source.Taxa);
            var nextId = source.Taxa[^1].Id;
            if (idAbove is not null && nextId <= idAbove.Value) break;
            idAbove = nextId;
            if (source.Taxa.Count < perPage) break;
        }

        var ordered = all
            .OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(taxon => taxon.ScientificName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _cache.Set(key, ordered, TimeSpan.FromMinutes(30));
        return ordered;
    }

    private async Task<(long Total, IReadOnlyList<SearchTaxon> Taxa)> FetchTaxaAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var client = _clients.CreateClient("inat");
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var total = GetLong(document.RootElement, "total_results") ?? 0;
        if (!TryArray(document.RootElement, "results", out var results))
            return (total, Array.Empty<SearchTaxon>());

        var taxa = results.EnumerateArray()
            .Select(ParseTaxon)
            .Where(taxon => taxon is not null)
            .Cast<SearchTaxon>()
            .ToList();
        return (total, taxa);
    }

    private async Task<TaxonomyResolution> ResolveTaxonomyAsync(
        string? order,
        string? family,
        string? genus,
        CancellationToken cancellationToken)
    {
        var requested = new List<(string Rank, string Name)>();
        if (!string.IsNullOrWhiteSpace(order)) requested.Add(("order", order.Trim()));
        if (!string.IsNullOrWhiteSpace(family)) requested.Add(("family", family.Trim()));
        if (!string.IsNullOrWhiteSpace(genus)) requested.Add(("genus", genus.Trim()));
        if (requested.Count == 0) return new TaxonomyResolution(true, AvesTaxonId);

        var resolved = new List<ResolvedTaxon>();
        foreach (var item in requested)
        {
            var taxon = await ResolveTaxonAsync(item.Rank, item.Name, cancellationToken);
            if (taxon is null) return new TaxonomyResolution(false, AvesTaxonId);
            resolved.Add(taxon);
        }

        var mostSpecific = resolved.OrderByDescending(taxon => RankWeight(taxon.Rank)).First();
        if (resolved.Any(taxon => taxon.Id != mostSpecific.Id
            && mostSpecific.Ancestors.Count > 0
            && !mostSpecific.Ancestors.Contains(taxon.Id)))
        {
            return new TaxonomyResolution(false, AvesTaxonId);
        }

        return new TaxonomyResolution(true, mostSpecific.Id);
    }

    private async Task<ResolvedTaxon?> ResolveTaxonAsync(
        string rank,
        string name,
        CancellationToken cancellationToken)
    {
        var key = $"bird-filter:{rank}:{name.ToLowerInvariant()}";
        if (_cache.TryGetValue<ResolvedTaxon>(key, out var cached)) return cached;

        var client = _clients.CreateClient("inat");
        var path = $"v1/taxa?taxon_id={AvesTaxonId}&rank={rank}&is_active=true&per_page=100&locale=en&q={Uri.EscapeDataString(name)}";
        using var response = await client.GetAsync(path, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!TryArray(document.RootElement, "results", out var results)) return null;

        foreach (var result in results.EnumerateArray())
        {
            if (!GetString(result, "name").Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!GetString(result, "rank").Equals(rank, StringComparison.OrdinalIgnoreCase)) continue;
            var id = GetLong(result, "id");
            if (id is null) continue;
            var resolved = new ResolvedTaxon(id.Value, rank, GetLongSet(result, "ancestor_ids"));
            _cache.Set(key, resolved, TimeSpan.FromHours(12));
            return resolved;
        }
        return null;
    }

    private async Task<GbifTaxon> MatchGbifAsync(string scientificName, CancellationToken cancellationToken)
    {
        var key = $"bird-gbif:{scientificName.ToLowerInvariant()}";
        if (_cache.TryGetValue<GbifTaxon>(key, out var cached)) return cached;

        try
        {
            var client = _clients.CreateClient("gbif");
            using var response = await client.GetAsync(
                $"v1/species/match?name={Uri.EscapeDataString(scientificName)}&strict=false",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return EmptyGbifTaxon();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var result = new GbifTaxon(
                GetLong(document.RootElement, "usageKey"),
                GetString(document.RootElement, "family"),
                GetString(document.RootElement, "order"),
                GetString(document.RootElement, "genus"));
            _cache.Set(key, result, TimeSpan.FromDays(3));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return EmptyGbifTaxon();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            return EmptyGbifTaxon();
        }
    }

    private async Task<bool> HasOccurrenceAsync(
        long gbifKey,
        string continent,
        CancellationToken cancellationToken)
    {
        var key = $"bird-continent:{gbifKey}:{continent}";
        if (_cache.TryGetValue<bool>(key, out var cached)) return cached;

        await _occurrenceGate.WaitAsync(cancellationToken);
        try
        {
            var client = _clients.CreateClient("gbif");
            using var response = await client.GetAsync(
                $"v1/occurrence/search?taxon_key={gbifKey}&continent={Uri.EscapeDataString(continent)}&limit=0",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var found = (GetLong(document.RootElement, "count") ?? 0) > 0;
            _cache.Set(key, found, TimeSpan.FromHours(18));
            return found;
        }
        finally
        {
            _occurrenceGate.Release();
        }
    }

    private static SearchTaxon? ParseTaxon(JsonElement element)
    {
        var id = GetLong(element, "id");
        var scientificName = GetString(element, "name");
        if (id is null || scientificName.Length == 0) return null;

        var preferred = GetString(element, "preferred_common_name");
        var dutch = LocalizedName(element, "nl", "Dutch");
        var english = LocalizedName(element, "en", "English");
        string? image = null;
        string? attribution = null;
        string? license = null;
        if (element.TryGetProperty("default_photo", out var photo) && photo.ValueKind == JsonValueKind.Object)
        {
            image = EmptyToNull(GetString(photo, "medium_url"));
            attribution = EmptyToNull(GetString(photo, "attribution"));
            license = EmptyToNull(GetString(photo, "license_code"));
        }

        var status = "NE";
        if (element.TryGetProperty("conservation_status", out var conservation)
            && conservation.ValueKind == JsonValueKind.Object)
        {
            status = GetString(conservation, "status").ToUpperInvariant();
            if (status.Length == 0) status = "NE";
        }

        return new SearchTaxon(
            id.Value,
            scientificName,
            dutch.Length > 0 ? dutch : preferred,
            english,
            image,
            attribution,
            license,
            status,
            GetLong(element, "observations_count") ?? 0);
    }

    private static string DisplayName(SearchTaxon taxon)
    {
        if (taxon.CommonName.Length > 0) return taxon.CommonName;
        if (taxon.EnglishName.Length > 0) return taxon.EnglishName;
        return taxon.ScientificName;
    }

    private static GbifTaxon EmptyGbifTaxon() =>
        new(null, string.Empty, string.Empty, string.Empty);

    private static string LocalizedName(JsonElement element, string locale, string lexicon)
    {
        if (!TryArray(element, "names", out var names)) return string.Empty;
        foreach (var name in names.EnumerateArray())
        {
            if (GetString(name, "locale").Equals(locale, StringComparison.OrdinalIgnoreCase)
                || GetString(name, "lexicon").Equals(lexicon, StringComparison.OrdinalIgnoreCase))
            {
                return GetString(name, "name");
            }
        }
        return string.Empty;
    }

    private static string? NormalizeContinent(string? continent)
    {
        if (string.IsNullOrWhiteSpace(continent)) return null;
        var normalized = continent.Trim().ToUpperInvariant().Replace('-', '_').Replace(' ', '_');
        return Continents.Contains(normalized) ? normalized : null;
    }

    private static int RankWeight(string rank) => rank.ToLowerInvariant() switch
    {
        "genus" => 3,
        "family" => 2,
        "order" => 1,
        _ => 0
    };

    private static bool TryArray(JsonElement element, string name, out JsonElement array)
    {
        if (element.TryGetProperty(name, out array) && array.ValueKind == JsonValueKind.Array) return true;
        array = default;
        return false;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static long? GetLong(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        return value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)
                ? number
                : null;
    }

    private static IReadOnlySet<long> GetLongSet(JsonElement element, string name)
    {
        if (!TryArray(element, name, out var values)) return new HashSet<long>();
        return values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _))
            .Select(value => value.GetInt64())
            .ToHashSet();
    }

    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private sealed record SearchTaxon(
        long Id,
        string ScientificName,
        string CommonName,
        string EnglishName,
        string? ImageUrl,
        string? PhotoAttribution,
        string? PhotoLicense,
        string IucnStatus,
        long ObservationCount);

    private sealed record GbifTaxon(long? Key, string Family, string Order, string Genus);
    private sealed record TaxonomyResolution(bool Valid, long TaxonId);
    private sealed record ResolvedTaxon(long Id, string Rank, IReadOnlySet<long> Ancestors);
}
