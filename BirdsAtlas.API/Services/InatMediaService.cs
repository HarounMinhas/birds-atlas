using BirdsAtlas.API.Data;
using BirdsAtlas.API.Models;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Fetches bird photos from iNaturalist API.
/// Docs: https://api.inaturalist.org/v1/docs/
/// Free, no authentication required for read access.
/// </summary>
public class InatMediaService
{
    private readonly HttpClient _http;
    private readonly BirdsDbContext _db;
    private readonly ILogger<InatMediaService> _logger;

    public InatMediaService(HttpClient http, BirdsDbContext db, ILogger<InatMediaService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task SyncPhotosForBirdAsync(Bird bird)
    {
        if (string.IsNullOrEmpty(bird.ScientificName)) return;

        try
        {
            // Search for taxa matching the scientific name
            var url = $"taxa?q={Uri.EscapeDataString(bird.ScientificName)}&rank=species&per_page=1";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            if (results.GetArrayLength() == 0) return;

            var taxon = results[0];
            bird.InatTaxonId = taxon.GetProperty("id").GetInt32();

            // Get default photo
            if (taxon.TryGetProperty("default_photo", out var photo))
            {
                var existingPrimary = _db.BirdMedia
                    .Any(m => m.BirdId == bird.Id && m.IsPrimary && m.Type == MediaType.Image);

                if (!existingPrimary)
                {
                    _db.BirdMedia.Add(new BirdMedia
                    {
                        Bird = bird,
                        Type = MediaType.Image,
                        Url = photo.TryGetProperty("medium_url", out var mu) ? mu.GetString() ?? string.Empty : string.Empty,
                        ThumbnailUrl = photo.TryGetProperty("square_url", out var su) ? su.GetString() : null,
                        Source = "inat",
                        IsPrimary = true,
                        License = photo.TryGetProperty("license_code", out var lc) ? lc.GetString() : null,
                        Attribution = photo.TryGetProperty("attribution", out var attr) ? attr.GetString() : null
                    });
                }
            }

            // Also enrich with iNaturalist characteristics if available
            if (taxon.TryGetProperty("wikipedia_url", out var wiki))
                bird.WikipediaUrl = wiki.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "iNaturalist media sync failed for {Name}", bird.ScientificName);
        }
    }
}
