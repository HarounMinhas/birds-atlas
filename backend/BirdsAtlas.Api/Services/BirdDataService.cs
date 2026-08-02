using System.Globalization;
using System.Net;
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
            wikipedia.Url,
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

        // Map data is not optional: upstream failures must remain HTTP errors so the
        // client can distinguish an outage from a genuine zero-result response.
        var gbif = await GetGbifMatchStrictAsync(taxon.ScientificName, cancellationToken);
        if (gbif.UsageKey is null) return Array.Empty<OccurrencePoint>();

        var client = _httpClientFactory.CreateClient("gbif");
        var path = $"v1/occurrence/search?taxon_key={gbif.UsageKey.Value}&has_coordinate=true&limit={limit}";
        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!TryGetArray(document.RootElement, "results", out var results))
            throw new JsonException("GBIF occurrence response omitted results.");

        var points = new List<OccurrencePoint>();
        var seenKeys = new HashSet<long>();
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object)
                throw new JsonException("GBIF occurrence response contained a non-object record.");

            var latitude = GetDouble(result, "decimalLatitude");
            var longitude = GetDouble(result, "decimalLongitude");
            if (latitude is null || longitude is null) continue;
            if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
                throw new JsonException("GBIF occurrence response contained invalid coordinates.");

            var key = GetLong(result, "key") ?? 0;
            if (key > 0 && !seenKeys.Add(key))
                throw new JsonException($"GBIF occurrence response contained duplicate key {key}.");

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
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!TryGetArray(document.RootElement, "results", out var results))
            throw new JsonException("iNaturalist taxon response omitted results.");

        var records = results.EnumerateArray().ToList();
        if (records.Count == 0) return null;
        if (records.Count != 1)
            throw new JsonException($"iNaturalist returned {records.Count} records for taxon {id}.");

        var taxon = ParseInatTaxon(records[0]);
        if (taxon.Id != id)
            throw new JsonException($"iNaturalist returned taxon {taxon.Id} while {id} was requested.");

        _cache.Set($"inat:{id}", taxon, TaxonCacheDuration);
        return taxon;
    }

    private async Task<GbifMatch> GetGbifMatchAsync(string scientificName, CancellationToken cancellationToken)
    {
        try
        {
            return await GetGbifMatchStrictAsync(scientificName, cancellationToken);
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

    private async Task<GbifMatch> GetGbifMatchStrictAsync(
        string scientificName,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"gbif-match:{scientificName.ToLowerInvariant()}";
        if (_cache.TryGetValue<GbifMatch>(cacheKey, out var cached)) return cached;

        var client = _httpClientFactory.CreateClient("gbif");
        using var response = await client.GetAsync(
            $"v1/species/match?name={Uri.EscapeDataString(scientificName)}&strict=false",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var match = ParseGbifMatch(document.RootElement, scientificName);
        _cache.Set(cacheKey, match, GbifCacheDuration);
        return match;
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
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("GBIF continent response was not an object.");

            var count = GetLong(document.RootElement, "count")
                ?? throw new JsonException("GBIF continent response omitted count.");
            if (count < 0)
                throw new JsonException("GBIF continent response contained a negative count.");
            if (count == 0)
            {
                var empty = Array.Empty<string>();
                _cache.Set(cacheKey, empty, TimeSpan.FromHours(18));
                return empty;
            }

            if (!TryGetArray(document.RootElement, "facets", out var facets))
                throw new JsonException("GBIF continent response omitted facets.");

            var continents = new List<string>();
            var foundContinentFacet = false;
            foreach (var facet in facets.EnumerateArray())
            {
                if (facet.ValueKind != JsonValueKind.Object)
                    throw new JsonException("GBIF continent response contained a non-object facet.");
                if (!GetString(facet, "field").Equals("CONTINENT", StringComparison.OrdinalIgnoreCase)) continue;

                foundContinentFacet = true;
                if (!TryGetArray(facet, "counts", out var counts))
                    throw new JsonException("GBIF continent facet omitted counts.");

                foreach (var facetCount in counts.EnumerateArray())
                {
                    if (facetCount.ValueKind != JsonValueKind.Object)
                        throw new JsonException("GBIF continent facet contained a non-object count.");
                    var name = GetString(facetCount, "name");
                    if (name.Length == 0)
                        throw new JsonException("GBIF continent facet count omitted name.");
                    var value = GetLong(facetCount, "count")
                        ?? throw new JsonException("GBIF continent facet count omitted count.");
                    if (value < 0)
                        throw new JsonException("GBIF continent facet contained a negative count.");
                    if (value > 0) continents.Add(name);
                }
            }

            if (!foundContinentFacet)
                throw new JsonException("GBIF continent response omitted the requested continent facet.");

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
        string? fallbackUrl = null;
        if (Uri.TryCreate(wikipediaUrl, UriKind.Absolute, out var sourceUri)
            && IsWikipediaUri(sourceUri))
        {
            candidates.Add(sourceUri);
            fallbackUrl = sourceUri.AbsoluteUri;
        }
        candidates.Add(new Uri($"https://en.wikipedia.org/wiki/{Uri.EscapeDataString(scientificName.Replace(' ', '_'))}"));

        var client = _httpClientFactory.CreateClient("wiki");
        foreach (var candidate in candidates.DistinctBy(uri => uri.AbsoluteUri))
        {
            try
            {
                var title = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (string.IsNullOrWhiteSpace(title)) continue;
                var summaryUri = new Uri($"https://{candidate.IdnHost}/api/rest_v1/page/summary/{title}");
                using var response = await client.GetAsync(summaryUri, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;

                var responseUri = response.RequestMessage?.RequestUri;
                if (responseUri is null || !IsWikipediaUri(responseUri))
                    throw new JsonException("Wikipedia summary request was redirected outside the Wikipedia domain.");

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Wikipedia summary response was not an object.");

                var summary = GetString(root, "extract");
                if (summary.Length == 0) continue;

                var pageUrl = candidate.AbsoluteUri;
                if (root.TryGetProperty("content_urls", out var contentUrls)
                    && contentUrls.ValueKind != JsonValueKind.Null)
                {
                    if (contentUrls.ValueKind != JsonValueKind.Object)
                        throw new JsonException("Wikipedia content_urls was not an object.");

                    if (contentUrls.TryGetProperty("desktop", out var desktop)
                        && desktop.ValueKind != JsonValueKind.Null)
                    {
                        if (desktop.ValueKind != JsonValueKind.Object)
                            throw new JsonException("Wikipedia content_urls.desktop was not an object.");

                        var returnedUrl = GetString(desktop, "page");
                        if (returnedUrl.Length > 0)
                        {
                            if (!Uri.TryCreate(returnedUrl, UriKind.Absolute, out var parsedUrl)
                                || !IsWikipediaUri(parsedUrl))
                            {
                                throw new JsonException("Wikipedia returned an invalid desktop page URL.");
                            }
                            pageUrl = parsedUrl.AbsoluteUri;
                        }
                    }
                }

                return (summary, pageUrl);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Wikipedia summary lookup timed out for {Url}", candidate);
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException)
            {
                _logger.LogDebug(exception, "Wikipedia summary lookup failed for {Url}", candidate);
            }
        }

        return (null, fallbackUrl);
    }

    private async Task<IReadOnlyList<AudioRecording>> GetRecordingsAsync(
        string scientificName,
        CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("XENO_CANTO_API_KEY")
            ?? _configuration["XenoCanto:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return Array.Empty<AudioRecording>();

        var nameParts = scientificName.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (nameParts.Length < 2) return Array.Empty<AudioRecording>();

        try
        {
            var client = _httpClientFactory.CreateClient("xeno");
            var search = Uri.EscapeDataString($"gen:{nameParts[0]} sp:{nameParts[1]}");
            using var response = await client.GetAsync(
                $"api/3/recordings?query={search}&key={Uri.EscapeDataString(apiKey)}",
                cancellationToken);
            if (!response.IsSuccessStatusCode) return Array.Empty<AudioRecording>();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException("Xeno-canto response was not an object.");
            if (!TryGetArray(document.RootElement, "recordings", out var recordings))
                return Array.Empty<AudioRecording>();

            var result = new List<AudioRecording>(3);
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var recording in recordings.EnumerateArray())
            {
                if (recording.ValueKind != JsonValueKind.Object)
                    throw new JsonException("Xeno-canto recordings contained a non-object element.");

                var id = GetString(recording, "id");
                if (id.Length == 0)
                    throw new JsonException("Xeno-canto recording omitted id.");
                if (!seenIds.Add(id))
                    throw new JsonException($"Xeno-canto response contained duplicate recording id {id}.");

                var file = GetString(recording, "file");
                if (file.Length == 0) continue;
                if (file.StartsWith("//", StringComparison.Ordinal)) file = "https:" + file;
                if (!Uri.TryCreate(file, UriKind.Absolute, out var fileUri) || !IsHttpUri(fileUri))
                    throw new JsonException($"Xeno-canto recording {id} contained an invalid file URL.");

                result.Add(new AudioRecording(
                    id,
                    GetString(recording, "en"),
                    string.Join(' ', new[] { GetString(recording, "gen"), GetString(recording, "sp") }.Where(value => value.Length > 0)),
                    GetString(recording, "rec"),
                    GetString(recording, "type"),
                    GetString(recording, "cnt"),
                    GetString(recording, "length"),
                    fileUri.AbsoluteUri,
                    GetString(recording, "lic"),
                    $"https://xeno-canto.org/{Uri.EscapeDataString(id)}"));

                if (result.Count == 3) break;
            }

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Xeno-canto lookup timed out for {ScientificName}", scientificName);
            return Array.Empty<AudioRecording>();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
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
            var seenIds = new HashSet<long>();
            long? expectedTotal = null;
            var page = 1;

            while (expectedTotal is null || seenIds.Count < expectedTotal.Value)
            {
                using var response = await client.GetAsync(
                    $"v1/taxa?taxon_id={AvesTaxonId}&rank={rank}&is_active=true&per_page={perPage}&page={page}&order_by=id&order=asc&locale=en",
                    cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var total = GetLong(document.RootElement, "total_results")
                    ?? throw new JsonException($"iNaturalist response for rank {rank} omitted total_results.");
                if (total < 0)
                    throw new JsonException($"iNaturalist response for rank {rank} contained a negative total_results value.");
                if (expectedTotal is null)
                    expectedTotal = total;
                else if (total != expectedTotal.Value)
                    throw new InvalidOperationException($"iNaturalist total_results changed while loading rank {rank}.");

                if (!TryGetArray(document.RootElement, "results", out var results))
                    throw new JsonException($"iNaturalist response for rank {rank} omitted results.");

                var pageCount = 0;
                foreach (var result in results.EnumerateArray())
                {
                    if (result.ValueKind != JsonValueKind.Object)
                        throw new JsonException($"iNaturalist taxonomy response for rank {rank} contained a non-object record.");

                    pageCount += 1;
                    var id = GetLong(result, "id")
                        ?? throw new JsonException($"iNaturalist taxonomy record for rank {rank} omitted id.");
                    if (!seenIds.Add(id))
                        throw new JsonException($"iNaturalist taxonomy pagination returned duplicate id {id} for rank {rank}.");

                    var resultRank = GetString(result, "rank");
                    if (!resultRank.Equals(rank, StringComparison.OrdinalIgnoreCase))
                        throw new JsonException($"iNaturalist taxonomy record {id} had rank {resultRank} instead of {rank}.");

                    var name = GetString(result, "name");
                    if (name.Length == 0)
                        throw new JsonException($"iNaturalist taxonomy record for rank {rank} omitted name.");
                    names.Add(name);
                }

                if (pageCount == 0 && seenIds.Count < expectedTotal.Value)
                    throw new InvalidOperationException($"iNaturalist taxonomy pagination stopped early for rank {rank}.");

                if (seenIds.Count > expectedTotal.Value)
                    throw new InvalidOperationException($"iNaturalist taxonomy pagination exceeded total_results for rank {rank}.");
                page += 1;
            }

            if (expectedTotal is null || seenIds.Count != expectedTotal.Value)
                throw new InvalidOperationException($"iNaturalist taxonomy pagination was incomplete for rank {rank}.");

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

    private static InatTaxon ParseInatTaxon(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("iNaturalist taxon response contained a non-object record.");

        var id = GetLong(element, "id")
            ?? throw new JsonException("iNaturalist taxon record omitted id.");
        var scientificName = GetString(element, "name");
        if (scientificName.Length == 0)
            throw new JsonException($"iNaturalist taxon record {id} omitted scientific name.");

        var observationCount = GetLong(element, "observations_count")
            ?? throw new JsonException($"iNaturalist taxon record {id} omitted observations_count.");
        if (observationCount < 0)
            throw new JsonException($"iNaturalist taxon record {id} contained a negative observations_count.");

        var preferred = GetString(element, "preferred_common_name");
        var dutch = GetLocalizedName(element, "nl", "Dutch");
        var english = GetLocalizedName(element, "en", "English");
        var commonName = dutch.Length > 0 ? dutch : preferred;

        string? imageUrl = null;
        string? attribution = null;
        string? license = null;
        if (element.TryGetProperty("default_photo", out var photo))
        {
            if (photo.ValueKind == JsonValueKind.Object)
            {
                imageUrl = NullIfEmpty(GetString(photo, "medium_url"));
                attribution = NullIfEmpty(GetString(photo, "attribution"));
                license = NullIfEmpty(GetString(photo, "license_code"));
            }
            else if (photo.ValueKind != JsonValueKind.Null)
            {
                throw new JsonException($"iNaturalist taxon record {id} contained an invalid default_photo.");
            }
        }

        var status = "NE";
        if (element.TryGetProperty("conservation_status", out var conservation))
        {
            if (conservation.ValueKind == JsonValueKind.Object)
            {
                status = GetString(conservation, "status").ToUpperInvariant();
                if (status.Length == 0) status = "NE";
            }
            else if (conservation.ValueKind != JsonValueKind.Null)
            {
                throw new JsonException($"iNaturalist taxon record {id} contained an invalid conservation_status.");
            }
        }

        return new InatTaxon(
            id,
            scientificName,
            commonName,
            english,
            imageUrl,
            attribution,
            license,
            status,
            observationCount,
            NullIfEmpty(GetString(element, "wikipedia_url")));
    }

    private static GbifMatch ParseGbifMatch(JsonElement root, string scientificName)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("GBIF species-match response was not an object.");

        var matchType = GetString(root, "matchType");
        if (matchType.Length == 0)
            throw new JsonException("GBIF species-match response omitted matchType.");
        if (matchType.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            return EmptyGbifMatch(scientificName);

        var rank = GetString(root, "rank");
        if (rank.Length == 0)
            throw new JsonException("GBIF species-match response omitted rank for a matched taxon.");
        if (!IsSuitableGbifSpeciesMatch(matchType, rank))
            return EmptyGbifMatch(scientificName);

        var usageKey = GetLong(root, "usageKey")
            ?? throw new JsonException("GBIF species-match response omitted usageKey for a matched species.");
        return new GbifMatch(
            usageKey,
            GetString(root, "kingdom"),
            GetString(root, "phylum"),
            GetString(root, "class"),
            GetString(root, "order"),
            GetString(root, "family"),
            GetString(root, "genus"),
            GetString(root, "species"));
    }

    private static string GetLocalizedName(JsonElement element, string locale, string lexicon)
    {
        if (!element.TryGetProperty("names", out var names) || names.ValueKind == JsonValueKind.Null)
            return string.Empty;
        if (names.ValueKind != JsonValueKind.Array)
            throw new JsonException("iNaturalist taxon property names was not an array.");

        foreach (var name in names.EnumerateArray())
        {
            if (name.ValueKind != JsonValueKind.Object)
                throw new JsonException("iNaturalist taxon names contained a non-object record.");

            var nameLocale = GetString(name, "locale");
            var nameLexicon = GetString(name, "lexicon");
            if (!nameLocale.Equals(locale, StringComparison.OrdinalIgnoreCase)
                && !nameLexicon.Equals(lexicon, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var localizedName = GetString(name, "name");
            if (localizedName.Length == 0)
                throw new JsonException($"iNaturalist {locale} name record omitted name.");
            return localizedName;
        }
        return string.Empty;
    }

    private static bool IsSuitableGbifSpeciesMatch(string matchType, string rank) =>
        rank.Equals("SPECIES", StringComparison.OrdinalIgnoreCase)
        && (matchType.Equals("EXACT", StringComparison.OrdinalIgnoreCase)
            || matchType.Equals("FUZZY", StringComparison.OrdinalIgnoreCase));

    private static GbifMatch EmptyGbifMatch(string scientificName) =>
        new(null, string.Empty, string.Empty, "Aves", string.Empty, string.Empty,
            scientificName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
            scientificName);

    private static bool TryGetArray(JsonElement element, string propertyName, out JsonElement array)
    {
        array = default;
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("JSON parent value was not an object.");
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return false;
        if (value.ValueKind != JsonValueKind.Array)
            throw new JsonException($"JSON property {propertyName} was not an array.");
        array = value;
        return true;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("JSON parent value was not an object.");
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return string.Empty;
        if (value.ValueKind != JsonValueKind.String)
            throw new JsonException($"JSON property {propertyName} was not a string.");
        return value.GetString() ?? string.Empty;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("JSON parent value was not an object.");
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }
        throw new JsonException($"JSON property {propertyName} was not a valid integer.");
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("JSON parent value was not an object.");
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }
        throw new JsonException($"JSON property {propertyName} was not a valid number.");
    }

    private static DateTimeOffset? GetDate(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException("JSON parent value was not an object.");
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text) || text.Contains('/'))
            return null;

        if (DateOnly.TryParseExact(
                text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateOnly))
        {
            return new DateTimeOffset(dateOnly.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        if (text.Length <= 10 || text[10] != 'T') return null;
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date)
            ? date
            : null;
    }

    private static bool IsWikipediaUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !uri.IsDefaultPort)
        {
            return false;
        }

        var host = uri.IdnHost.TrimEnd('.');
        return host.Equals("wikipedia.org", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".wikipedia.org", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHttpUri(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
