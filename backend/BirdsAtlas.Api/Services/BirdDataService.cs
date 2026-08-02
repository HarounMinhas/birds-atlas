using System.Globalization;
using System.Text.Json;
using BirdsAtlas.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsAtlas.Api.Services;

public sealed class BirdDataService
{
    private const long AvesTaxonId = 3;
    private static readonly TimeSpan TaxonCacheDuration = TimeSpan.FromHours(12);
    private static readonly TimeSpan GbifCacheDuration = TimeSpan.FromDays(3);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BirdDataService> _logger;

    public BirdDataService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<BirdDataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<BirdDetail?> GetBirdAsync(long id, CancellationToken cancellationToken)
    {
        var taxon = await GetInatTaxonAsync(id, cancellationToken);
        if (taxon is null) return null;

        var gbif = await GetGbifMatchAsync(taxon.ScientificName, cancellationToken);
        var wikipediaTask = GetWikipediaSummaryAsync(taxon.WikipediaUrl, taxon.ScientificName, cancellationToken);
        var continentsTask = gbif.UsageKey is null
            ? Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>())
            : GetContinentsAsync(gbif.UsageKey.Value, cancellationToken);
        var recordingsTask = GetRecordingsAsync(taxon.ScientificName, cancellationToken);

        await Task.WhenAll(wikipediaTask, continentsTask, recordingsTask);
        var wikipedia = await wikipediaTask;
        var continents = await continentsTask;
        var recordings = await recordingsTask;

        return new BirdDetail(
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
            gbif.UsageKey,
            wikipedia.Summary,
            wikipedia.Url ?? taxon.WikipediaUrl,
            $"https://www.inaturalist.org/taxa/{taxon.Id}",
            gbif.UsageKey is null ? null : $"https://www.gbif.org/species/{gbif.UsageKey}",
            continents,
            new BirdTaxonomy(
                gbif.Kingdom,
                gbif.Phylum,
                gbif.ClassName,
                gbif.Order,
                gbif.Family,
                gbif.Genus,
                gbif.Species.Length == 0 ? taxon.ScientificName : gbif.Species,
                gbif.UsageKey),
            recordings);
    }

    public async Task<IReadOnlyList<OccurrencePoint>> GetOccurrencesAsync(
        long inatId,
        int limit,
        CancellationToken cancellationToken)
    {
        var taxon = await GetInatTaxonAsync(inatId, cancellationToken);
        if (taxon is null) return Array.Empty<OccurrencePoint>();

        var gbif = await GetGbifMatchAsync(taxon.ScientificName, cancellationToken);
        if (gbif.UsageKey is null) return Array.Empty<OccurrencePoint>();

        try
        {
            var client = _httpClientFactory.CreateClient("gbif");
            var path = $"v1/occurrence/search?taxon_key={gbif.UsageKey.Value}&has_coordinate=true&limit={limit}";
            using var response = await client.GetAsync(path, cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<OccurrencePoint>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryGetArray(document.RootElement, "results", out var results)) return Array.Empty<OccurrencePoint>();

            var points = new List<OccurrencePoint>();
            foreach (var result in results.EnumerateArray())
            {
                var latitude = GetDouble(result, "decimalLatitude");
                var longitude = GetDouble(result, "decimalLongitude");
                if (latitude is null || longitude is null) continue;

                var key = GetLong(result, "key") ?? 0;
                points.Add(new OccurrencePoint(
                    key,
                    latitude.Value,
                    longitude.Value,
                    GetString(result, "country"),
                    GetString(result, "locality"),
                    GetDate(result, "eventDate"),
                    key > 0 ? $"https://www.gbif.org/occurrence/{key}" : $"https://www.gbif.org/species/{gbif.UsageKey}"));
            }

            return points;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GBIF occurrence lookup timed out for iNaturalist taxon {TaxonId}", inatId);
            return Array.Empty<OccurrencePoint>();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "GBIF occurrence lookup failed for iNaturalist taxon {TaxonId}", inatId);
            return Array.Empty<OccurrencePoint>();
        }
    }

    public async Task<TaxonomyOptions> GetTaxonomyOptionsAsync(CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync("taxonomy-options", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            var ordersTask = FetchTaxonomyRankAsync("order", 100, cancellationToken);
            var familiesTask = FetchTaxonomyRankAsync("family", 200, cancellationToken);
            var generaTask = FetchTaxonomyRankAsync("genus", 200, cancellationToken);
            await Task.WhenAll(ordersTask, familiesTask, generaTask);
            return new TaxonomyOptions(await ordersTask, await familiesTask, await generaTask);
        }) ?? new TaxonomyOptions(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
    }

    private async Task<InatTaxon?> GetInatTaxonAsync(long id, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue<InatTaxon>($"inat:{id}", out var cached)) return cached;

        var client = _httpClientFactory.CreateClient("inat");
        using var response = await client.GetAsync($"v1/taxa/{id}?locale=nl&all_names=true", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!TryGetArray(document.RootElement, "results", out var results)) return null;
        var first = results.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined) return null;

        var taxon = ParseInatTaxon(first);
        if (taxon is not null) _cache.Set($"inat:{id}", taxon, TaxonCacheDuration);
        return taxon;
    }

    private async Task<GbifMatch> GetGbifMatchAsync(string scientificName, CancellationToken cancellationToken)
    {
        var cacheKey = $"gbif-match:{scientificName.ToLowerInvariant()}";
        if (_cache.TryGetValue<GbifMatch>(cacheKey, out var cached)) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("gbif");
            using var response = await client.GetAsync(
                $"v1/species/match?name={Uri.EscapeDataString(scientificName)}&strict=false",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return EmptyGbifMatch(scientificName);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var match = new GbifMatch(
                GetLong(root, "usageKey"),
                GetString(root, "kingdom"),
                GetString(root, "phylum"),
                GetString(root, "class"),
                GetString(root, "order"),
                GetString(root, "family"),
                GetString(root, "genus"),
                GetString(root, "species"));
            _cache.Set(cacheKey, match, GbifCacheDuration);
            return match;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GBIF match timed out for {ScientificName}", scientificName);
            return EmptyGbifMatch(scientificName);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "GBIF match failed for {ScientificName}", scientificName);
            return EmptyGbifMatch(scientificName);
        }
    }

    private async Task<IReadOnlyList<string>> GetContinentsAsync(long gbifKey, CancellationToken cancellationToken)
    {
        var cacheKey = $"gbif-continents:{gbifKey}";
        if (_cache.TryGetValue<IReadOnlyList<string>>(cacheKey, out var cached)) return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("gbif");
            using var response = await client.GetAsync(
                $"v1/occurrence/search?taxon_key={gbifKey}&limit=0&facet=continent&facet_limit=20",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var continents = new List<string>();
            if (TryGetArray(document.RootElement, "facets", out var facets))
            {
                foreach (var facet in facets.EnumerateArray())
                {
                    if (!GetString(facet, "field").Equals("CONTINENT", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!TryGetArray(facet, "counts", out var counts)) continue;
                    continents.AddRange(counts.EnumerateArray()
                        .Select(count => GetString(count, "name"))
                        .Where(name => name.Length > 0));
                }
            }

            var result = continents.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
            _cache.Set(cacheKey, result, TimeSpan.FromHours(18));
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("GBIF continent lookup timed out for taxon {GbifKey}", gbifKey);
            return Array.Empty<string>();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning(exception, "GBIF continent lookup failed for taxon {GbifKey}", gbifKey);
            return Array.Empty<string>();
        }
    }

    private async Task<(string? Summary, string? Url)> GetWikipediaSummaryAsync(
        string? wikipediaUrl,
        string scientificName,
        CancellationToken cancellationToken)
    {
        var candidates = new List<Uri>();
        if (Uri.TryCreate(wikipediaUrl, UriKind.Absolute, out var sourceUri)
            && sourceUri.Host.EndsWith("wikipedia.org", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(sourceUri);
        }
        candidates.Add(new Uri($"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(scientificName.Replace(' ', '_'))}"));

        var client = _httpClientFactory.CreateClient("wiki");
        foreach (var candidate in candidates.DistinctBy(uri => uri.AbsoluteUri))
        {
            try
            {
                var title = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (string.IsNullOrWhiteSpace(title)) continue;
                var summaryUri = new Uri($"{candidate.Scheme}://{candidate.Host}/api/rest_v1/page/summary/{title}");
                using var response = await client.GetAsync(summaryUri, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var summary = GetString(document.RootElement, "extract");
                if (summary.Length == 0) continue;
                var pageUrl = candidate.AbsoluteUri;
                if (document.RootElement.TryGetProperty("content_urls", out var contentUrls)
                    && contentUrls.TryGetProperty("desktop", out var desktop))
                {
                    pageUrl = GetString(desktop, "page") is { Length: > 0 } url ? url : pageUrl;
                }
                return (summary, pageUrl);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                _logger.LogDebug(exception, "Wikipedia summary lookup failed for {Url}", candidate);
            }
        }

        return (null, wikipediaUrl);
    }

    private async Task<IReadOnlyList<AudioRecording>> GetRecordingsAsync(
        string scientificName,
        CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("XENO_CANTO_API_KEY")
            ?? _configuration["XenoCanto:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return Array.Empty<AudioRecording>();

        try
        {
            var client = _httpClientFactory.CreateClient("xeno");
            var search = Uri.EscapeDataString($"sp:\"{scientificName}\"");
            using var response = await client.GetAsync(
                $"api/3/recordings?query={search}&key={Uri.EscapeDataString(apiKey)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<AudioRecording>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!TryGetArray(document.RootElement, "recordings", out var recordings))
                return Array.Empty<AudioRecording>();

            return recordings.EnumerateArray().Take(3).Select(recording =>
            {
                var file = GetString(recording, "file");
                if (file.StartsWith("//", StringComparison.Ordinal)) file = "https:" + file;
                var id = GetString(recording, "id");
                return new AudioRecording(
                    id,
                    GetString(recording, "en"),
                    string.Join(' ', new[] { GetString(recording, "gen"), GetString(recording, "sp") }.Where(value => value.Length > 0)),
                    GetString(recording, "rec"),
                    GetString(recording, "type"),
                    GetString(recording, "cnt"),
                    GetString(recording, "length"),
                    file,
                    GetString(recording, "lic"),
                    id.Length == 0 ? "https://xeno-canto.org" : $"https://xeno-canto.org/{id}");
            }).Where(recording => recording.FileUrl.Length > 0).ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Xeno-canto lookup failed for {ScientificName}", scientificName);
            return Array.Empty<AudioRecording>();
        }
    }

    private async Task<IReadOnlyList<string>> FetchTaxonomyRankAsync(
        string rank,
        int perPage,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("inat");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = long.MaxValue;
            long processed = 0;
            var page = 1;

            while (processed < total)
            {
                using var response = await client.GetAsync(
                    $"v1/taxa?taxon_id={AvesTaxonId}&rank={rank}&is_active=true&per_page={perPage}&page={page}&order_by=id&order=asc&locale=en",
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                total = GetLong(document.RootElement, "total_results")
                    ?? throw new JsonException($"iNaturalist response for rank {rank} omitted total_results.");
                if (!TryGetArray(document.RootElement, "results", out var results))
                    throw new JsonException($"iNaturalist response for rank {rank} omitted results.");

                var pageCount = 0;
                foreach (var result in results.EnumerateArray())
                {
                    pageCount += 1;
                    var name = GetString(result, "name");
                    if (name.Length == 0)
                        throw new JsonException($"iNaturalist taxonomy record for rank {rank} omitted name.");
                    names.Add(name);
                }

                if (pageCount == 0 && processed < total)
                    throw new InvalidOperationException($"iNaturalist taxonomy pagination stopped early for rank {rank}.");

                processed += pageCount;
                page += 1;
            }

            return names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Taxonomy option lookup timed out for rank {Rank}", rank);
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Taxonomy option lookup failed for rank {Rank}", rank);
            throw;
        }
    }

    private static InatTaxon? ParseInatTaxon(JsonElement element)
    {
        var id = GetLong(element, "id");
        var scientificName = GetString(element, "name");
        if (id is null || scientificName.Length == 0) return null;

        var preferred = GetString(element, "preferred_common_name");
        var dutch = GetLocalizedName(element, "nl", "Dutch");
        var english = GetLocalizedName(element, "en", "English");
        var commonName = dutch.Length > 0 ? dutch : preferred;

        string? imageUrl = null;
        string? attribution = null;
        string? license = null;
        if (element.TryGetProperty("default_photo", out var photo) && photo.ValueKind == JsonValueKind.Object)
        {
            imageUrl = NullIfEmpty(GetString(photo, "medium_url"));
            attribution = NullIfEmpty(GetString(photo, "attribution"));
            license = NullIfEmpty(GetString(photo, "license_code"));
        }

        var status = "NE";
        if (element.TryGetProperty("conservation_status", out var conservation)
            && conservation.ValueKind == JsonValueKind.Object)
        {
            status = GetString(conservation, "status").ToUpperInvariant();
            if (status.Length == 0) status = "NE";
        }

        return new InatTaxon(
            id.Value,
            scientificName,
            commonName,
            english,
            imageUrl,
            attribution,
            license,
            status,
            GetLong(element, "observations_count") ?? 0,
            NullIfEmpty(GetString(element, "wikipedia_url")));
    }

    private static string GetLocalizedName(JsonElement element, string locale, string lexicon)
    {
        if (!TryGetArray(element, "names", out var names)) return string.Empty;
        foreach (var name in names.EnumerateArray())
        {
            var nameLocale = GetString(name, "locale");
            var nameLexicon = GetString(name, "lexicon");
            if (nameLocale.Equals(locale, StringComparison.OrdinalIgnoreCase)
                || nameLexicon.Equals(lexicon, StringComparison.OrdinalIgnoreCase))
            {
                return GetString(name, "name");
            }
        }
        return string.Empty;
    }

    private static GbifMatch EmptyGbifMatch(string scientificName) =>
        new(null, string.Empty, string.Empty, "Aves", string.Empty, string.Empty,
            scientificName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
            scientificName);

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        if (element.TryGetProperty(propertyName, out array) && array.ValueKind == JsonValueKind.Array)
            return true;
        array = default;
        return false;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return string.Empty;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private static DateTimeOffset? GetDate(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
