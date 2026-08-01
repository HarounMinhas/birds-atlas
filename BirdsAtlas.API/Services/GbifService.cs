using BirdsAtlas.API.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Wraps GBIF Species + Occurrence APIs.
/// Free, no auth. https://techdocs.gbif.org/en/openapi/
/// Class Aves taxon key = 212.
/// </summary>
public class GbifService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GbifService> _log;
    private const int AvesTaxonKey = 212;

    public static readonly List<string> AllContinents =
        ["EUROPE", "AFRICA", "ASIA", "NORTH_AMERICA", "SOUTH_AMERICA", "OCEANIA", "ANTARCTICA"];

    public GbifService(HttpClient http, IMemoryCache cache, ILogger<GbifService> log)
        => (_http, _cache, _log) = (http, cache, log);

    // ── Search ────────────────────────────────────────────────────────────
    public async Task<(List<GbifSpecies> Items, long Total)> SearchAsync(BirdSearchRequest req)
    {
        var key = $"gbif_search_{req}";
        if (_cache.TryGetValue(key, out (List<GbifSpecies>, long) hit)) return hit;

        var url = $"species/search?highertaxonKey={AvesTaxonKey}&rank=SPECIES&status=ACCEPTED" +
                  $"&limit={req.Limit}&offset={req.Offset}";

        if (!string.IsNullOrWhiteSpace(req.Search))
            url += $"&q={Uri.EscapeDataString(req.Search)}";
        if (!string.IsNullOrWhiteSpace(req.Order))
            url += $"&higherTaxon={Uri.EscapeDataString(req.Order)}";
        if (!string.IsNullOrWhiteSpace(req.Family))
            url += $"&higherTaxon={Uri.EscapeDataString(req.Family)}";
        if (!string.IsNullOrWhiteSpace(req.Genus))
            url += $"&higherTaxon={Uri.EscapeDataString(req.Genus)}";

        try
        {
            var json = await _http.GetStringAsync(url);
            var doc = JsonDocument.Parse(json);
            var total = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt64() : 0;
            var items = doc.RootElement.GetProperty("results")
                .EnumerateArray().Select(Parse).OfType<GbifSpecies>().ToList();
            var result = (items, total);
            _cache.Set(key, result, TimeSpan.FromMinutes(30));
            return result;
        }
        catch (Exception ex) { _log.LogError(ex, "GBIF search failed: {Url}", url); return ([], 0); }
    }

    // ── Detail ────────────────────────────────────────────────────────────
    public async Task<GbifSpecies?> GetSpeciesAsync(int taxonKey)
    {
        var key = $"gbif_sp_{taxonKey}";
        if (_cache.TryGetValue(key, out GbifSpecies? hit)) return hit;
        try
        {
            var json = await _http.GetStringAsync($"species/{taxonKey}");
            var sp = Parse(JsonDocument.Parse(json).RootElement);
            _cache.Set(key, sp, TimeSpan.FromHours(1));
            return sp;
        }
        catch (Exception ex) { _log.LogError(ex, "GBIF species {K} failed", taxonKey); return null; }
    }

    // ── Continent distribution (parallel occurrence checks) ───────────────
    public async Task<List<string>> GetContinentsAsync(int taxonKey)
    {
        var key = $"gbif_cont_{taxonKey}";
        if (_cache.TryGetValue(key, out List<string>? hit)) return hit ?? [];
        var results = await Task.WhenAll(AllContinents.Select(async c =>
        {
            try
            {
                var j = await _http.GetStringAsync(
                    $"occurrence/search?taxonKey={taxonKey}&continent={c}&limit=1");
                return JsonDocument.Parse(j).RootElement.GetProperty("count").GetInt64() > 0 ? c : null;
            }
            catch { return null; }
        }));
        var continents = results.OfType<string>().ToList();
        _cache.Set(key, continents, TimeSpan.FromHours(2));
        return continents;
    }

    // ── Taxonomy helpers ──────────────────────────────────────────────────
    public async Task<List<string>> GetOrdersAsync()
    {
        const string key = "gbif_orders";
        if (_cache.TryGetValue(key, out List<string>? hit)) return hit ?? [];
        try
        {
            var json = await _http.GetStringAsync($"species/{AvesTaxonKey}/children?limit=200");
            var orders = JsonDocument.Parse(json).RootElement.GetProperty("results")
                .EnumerateArray()
                .Where(r => r.TryGetProperty("rank", out var rank) && rank.GetString() == "ORDER")
                .Select(r => r.TryGetProperty("canonicalName", out var n) ? n.GetString() : null)
                .OfType<string>().OrderBy(x => x).ToList();
            _cache.Set(key, orders, TimeSpan.FromHours(6));
            return orders;
        }
        catch { return []; }
    }

    // ── Parser ────────────────────────────────────────────────────────────
    private static GbifSpecies? Parse(JsonElement e)
    {
        if (!e.TryGetProperty("key", out var k)) return null;
        return new GbifSpecies
        {
            TaxonKey       = k.GetInt32(),
            ScientificName = e.TryGetProperty("scientificName",  out var sn) ? sn.GetString() ?? "" : "",
            CanonicalName  = e.TryGetProperty("canonicalName",   out var cn) ? cn.GetString() ?? "" : "",
            VernacularName = e.TryGetProperty("vernacularName",  out var vn) ? vn.GetString()        : null,
            Order          = e.TryGetProperty("order",           out var o)  ? o.GetString()  ?? "" : "",
            Family         = e.TryGetProperty("family",          out var f)  ? f.GetString()  ?? "" : "",
            Genus          = e.TryGetProperty("genus",           out var g)  ? g.GetString()  ?? "" : "",
            ConservationStatus = e.TryGetProperty("iucnRedListCategory", out var iucn) ? iucn.GetString() : null
        };
    }
}

public class GbifSpecies
{
    public int TaxonKey { get; set; }
    public string ScientificName  { get; set; } = "";
    public string CanonicalName   { get; set; } = "";
    public string? VernacularName { get; set; }
    public string Order  { get; set; } = "";
    public string Family { get; set; } = "";
    public string Genus  { get; set; } = "";
    public string? ConservationStatus { get; set; }
}
