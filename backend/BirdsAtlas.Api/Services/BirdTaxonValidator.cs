using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace BirdsAtlas.Api.Services;

public sealed class BirdTaxonValidator
{
    private const long AvesTaxonId = 3;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;

    public BirdTaxonValidator(IHttpClientFactory httpClientFactory, IMemoryCache cache)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
    }

    public async Task<bool> IsActiveBirdSpeciesAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0) return false;

        var cacheKey = $"inat-bird-species:{id}";
        if (_cache.TryGetValue<bool>(cacheKey, out var cached)) return cached;

        var client = _httpClientFactory.CreateClient("inat");
        using var response = await client.GetAsync($"v1/taxa/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            _cache.Set(cacheKey, false, TimeSpan.FromMinutes(30));
            return false;
        }

        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("results", out var results)
            || results.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("iNaturalist taxon validation response omitted results.");
        }

        var records = results.EnumerateArray().ToList();
        if (records.Count != 1 || records[0].ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"iNaturalist returned {records.Count} validation records for taxon {id}.");
        }

        var taxon = records[0];
        var returnedId = GetLong(taxon, "id");
        var rank = GetString(taxon, "rank");
        var active = GetBoolean(taxon, "is_active");
        var ancestors = GetLongSet(taxon, "ancestor_ids");

        var valid = returnedId == id
            && active
            && rank.Equals("species", StringComparison.OrdinalIgnoreCase)
            && ancestors.Contains(AvesTaxonId);

        _cache.Set(cacheKey, valid, valid ? CacheDuration : TimeSpan.FromMinutes(30));
        return valid;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (value.ValueKind != JsonValueKind.String)
            throw new JsonException($"JSON property {propertyName} was not a string.");
        return value.GetString() ?? string.Empty;
    }

    private static long? GetLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
            return number;
        throw new JsonException($"JSON property {propertyName} was not an integer.");
    }

    private static bool GetBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();
        throw new JsonException($"JSON property {propertyName} was not a boolean.");
    }

    private static IReadOnlySet<long> GetLongSet(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var values)
            || values.ValueKind == JsonValueKind.Null)
        {
            return new HashSet<long>();
        }

        if (values.ValueKind != JsonValueKind.Array)
            throw new JsonException($"JSON property {propertyName} was not an array.");

        var result = new HashSet<long>();
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number))
                throw new JsonException($"JSON property {propertyName} contained a non-integer value.");
            result.Add(number);
        }
        return result;
    }
}
