using BirdsAtlas.API.Data;
using BirdsAtlas.API.Models;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Fetches bird sound recordings from Xeno-canto API.
/// Docs: https://xeno-canto.org/explore/api
/// Free, no authentication required.
/// </summary>
public class XenoCantoService
{
    private readonly HttpClient _http;
    private readonly BirdsDbContext _db;
    private readonly ILogger<XenoCantoService> _logger;

    public XenoCantoService(HttpClient http, BirdsDbContext db, ILogger<XenoCantoService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task SyncSoundsForBirdAsync(Bird bird)
    {
        if (string.IsNullOrEmpty(bird.ScientificName)) return;

        try
        {
            // Xeno-canto query by scientific name, limit to 2 recordings
            var name = bird.ScientificName.Replace(" ", "+");
            var url = $"recordings?query={Uri.EscapeDataString(bird.ScientificName)}&page=1";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("recordings", out var recordings)) return;

            int added = 0;
            foreach (var rec in recordings.EnumerateArray())
            {
                if (added >= 2) break; // Only store top 2 sounds per bird

                var fileUrl = rec.TryGetProperty("file", out var f) ? f.GetString() : null;
                if (string.IsNullOrEmpty(fileUrl)) continue;

                var exists = _db.BirdMedia.Any(m => m.BirdId == bird.Id && m.Url == fileUrl);
                if (!exists)
                {
                    _db.BirdMedia.Add(new BirdMedia
                    {
                        Bird = bird,
                        Type = MediaType.Audio,
                        Url = fileUrl.StartsWith("//") ? "https:" + fileUrl : fileUrl,
                        Source = "xeno-canto",
                        License = rec.TryGetProperty("lic", out var lic) ? lic.GetString() : null,
                        Attribution = rec.TryGetProperty("rec", out var recName) ? recName.GetString() : null
                    });
                    added++;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xeno-canto sync failed for {Name}", bird.ScientificName);
        }
    }
}
