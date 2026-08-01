using BirdsAtlas.API.DTOs;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Fetches bird sound recordings from Xeno-canto.
/// Free, no auth. https://xeno-canto.org/explore/api
/// </summary>
public class XenoCantoService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<XenoCantoService> _log;

    public XenoCantoService(HttpClient http, IMemoryCache cache, ILogger<XenoCantoService> log)
        => (_http, _cache, _log) = (http, cache, log);

    public async Task<List<MediaDto>> GetSoundsAsync(string scientificName, int max = 2)
    {
        var key = $"xc_{scientificName}";
        if (_cache.TryGetValue(key, out List<MediaDto>? hit)) return hit ?? [];

        var sounds = new List<MediaDto>();
        try
        {
            var url  = $"recordings?query={Uri.EscapeDataString(scientificName)}&page=1";
            var json = await _http.GetStringAsync(url);
            var doc  = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("recordings", out var recs)) return sounds;

            foreach (var r in recs.EnumerateArray().Take(max))
            {
                var file = r.TryGetProperty("file", out var f) ? f.GetString() : null;
                if (string.IsNullOrEmpty(file)) continue;
                sounds.Add(new MediaDto(
                    Url:         file.StartsWith("//") ? "https:" + file : file,
                    Source:      "xeno-canto",
                    Attribution: r.TryGetProperty("rec",  out var rec)  ? rec.GetString()  : null,
                    License:     r.TryGetProperty("lic",  out var lic)  ? lic.GetString()  : null
                ));
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Xeno-canto failed: {Name}", scientificName); }

        _cache.Set(key, sounds, TimeSpan.FromHours(1));
        return sounds;
    }
}
