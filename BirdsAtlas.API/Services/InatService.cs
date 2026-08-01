using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Fetches photos + descriptions from iNaturalist API.
/// Free, no auth. https://api.inaturalist.org/v1/docs/
/// </summary>
public class InatService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<InatService> _log;

    public InatService(HttpClient http, IMemoryCache cache, ILogger<InatService> log)
        => (_http, _cache, _log) = (http, cache, log);

    public async Task<InatTaxon?> GetByNameAsync(string scientificName)
    {
        var key = $"inat_{scientificName}";
        if (_cache.TryGetValue(key, out InatTaxon? hit)) return hit;
        try
        {
            var url = $"taxa?q={Uri.EscapeDataString(scientificName)}&rank=species&per_page=1&order_by=observations_count";
            var json = await _http.GetStringAsync(url);
            var doc  = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");
            if (results.GetArrayLength() == 0) return null;

            var t = results[0];
            var taxon = new InatTaxon
            {
                Id                  = t.GetProperty("id").GetInt32(),
                Name                = t.TryGetProperty("name",                  out var n)   ? n.GetString()   ?? "" : "",
                PreferredCommonName = t.TryGetProperty("preferred_common_name", out var pcn) ? pcn.GetString()       : null,
                WikipediaUrl        = t.TryGetProperty("wikipedia_url",         out var wu)  ? wu.GetString()        : null,
                WikipediaSummary    = t.TryGetProperty("wikipedia_summary",     out var ws)  ? ws.GetString()        : null,
            };

            if (t.TryGetProperty("default_photo", out var photo))
            {
                taxon.ImageUrl      = photo.TryGetProperty("medium_url", out var mu) ? mu.GetString() : null;
                taxon.ThumbnailUrl  = photo.TryGetProperty("square_url", out var su) ? su.GetString() : null;
                taxon.PhotoLicense  = photo.TryGetProperty("license_code", out var lc) ? lc.GetString() : null;
                taxon.Attribution   = photo.TryGetProperty("attribution",   out var at) ? at.GetString() : null;
            }
            if (t.TryGetProperty("conservation_status", out var cs))
                taxon.ConservationStatus = cs.TryGetProperty("status", out var s) ? s.GetString()?.ToUpper() : null;

            _cache.Set(key, taxon, TimeSpan.FromHours(1));
            return taxon;
        }
        catch (Exception ex) { _log.LogWarning(ex, "iNat lookup failed: {Name}", scientificName); return null; }
    }
}

public class InatTaxon
{
    public int    Id                  { get; set; }
    public string Name                { get; set; } = "";
    public string? PreferredCommonName { get; set; }
    public string? ImageUrl            { get; set; }
    public string? ThumbnailUrl        { get; set; }
    public string? WikipediaUrl        { get; set; }
    public string? WikipediaSummary    { get; set; }
    public string? ConservationStatus  { get; set; }
    public string? PhotoLicense        { get; set; }
    public string? Attribution         { get; set; }
}
