using BirdsAtlas.API.Data;
using BirdsAtlas.API.Models;
using System.Text.Json;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Syncs bird species from GBIF Species API (class Aves) into the local database.
/// Runs daily via Hangfire. GBIF API is free and requires no authentication.
/// Docs: https://techdocs.gbif.org/en/openapi/
/// </summary>
public class GbifSyncService
{
    private readonly HttpClient _http;
    private readonly BirdsDbContext _db;
    private readonly ILogger<GbifSyncService> _logger;

    // GBIF taxon key for class Aves (all birds)
    private const int AvesTaxonKey = 212;

    // Continent codes mapped to GBIF continent strings
    private static readonly Dictionary<string, string> ContinentMap = new()
    {
        { "EUROPE", "EUROPE" },
        { "AFRICA", "AFRICA" },
        { "ASIA", "ASIA" },
        { "NORTH_AMERICA", "NORTH_AMERICA" },
        { "SOUTH_AMERICA", "SOUTH_AMERICA" },
        { "OCEANIA", "OCEANIA" }
    };

    public GbifSyncService(HttpClient http, BirdsDbContext db, ILogger<GbifSyncService> logger)
    {
        _http = http;
        _db = db;
        _logger = logger;
    }

    public async Task SyncAllBirdsAsync()
    {
        _logger.LogInformation("Starting GBIF sync at {Time}", DateTime.UtcNow);

        int offset = 0;
        int limit = 100;
        bool hasMore = true;

        while (hasMore)
        {
            try
            {
                // GBIF Species API: list species under class Aves
                var url = $"species/{AvesTaxonKey}/children?limit={limit}&offset={offset}";
                var response = await _http.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");
                hasMore = !doc.RootElement.GetProperty("endOfRecords").GetBoolean();

                foreach (var item in results.EnumerateArray())
                {
                    await UpsertBirdFromGbif(item);
                }

                offset += limit;
                await Task.Delay(200); // Respectful rate limiting
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GBIF sync error at offset {Offset}", offset);
                break;
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("GBIF sync completed at {Time}", DateTime.UtcNow);
    }

    private async Task UpsertBirdFromGbif(JsonElement item)
    {
        if (!item.TryGetProperty("scientificName", out var snProp)) return;
        var scientificName = snProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(scientificName)) return;

        var existing = _db.Birds.FirstOrDefault(b => b.ScientificName == scientificName);
        var bird = existing ?? new Bird();

        bird.ScientificName = scientificName;
        bird.CommonNameEn = item.TryGetProperty("vernacularName", out var vn) ? vn.GetString() ?? string.Empty : string.Empty;
        bird.Order = item.TryGetProperty("order", out var o) ? o.GetString() ?? string.Empty : string.Empty;
        bird.Family = item.TryGetProperty("family", out var f) ? f.GetString() ?? string.Empty : string.Empty;
        bird.Genus = item.TryGetProperty("genus", out var g) ? g.GetString() ?? string.Empty : string.Empty;
        bird.GbifTaxonKey = item.TryGetProperty("key", out var k) ? k.GetInt32() : null;
        bird.LastSynced = DateTime.UtcNow;

        if (existing is null) _db.Birds.Add(bird);

        // Sync continent distribution for this species
        if (bird.GbifTaxonKey.HasValue)
            await SyncContinentsAsync(bird);
    }

    private async Task SyncContinentsAsync(Bird bird)
    {
        foreach (var (code, gbifCode) in ContinentMap)
        {
            var url = $"occurrence/search?taxonKey={bird.GbifTaxonKey}&continent={gbifCode}&limit=1";
            var response = await _http.GetAsync(url);
            if (!response.IsSuccessStatusCode) continue;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var count = doc.RootElement.GetProperty("count").GetInt64();

            if (count > 0)
            {
                var exists = _db.BirdContinents.Any(bc => bc.BirdId == bird.Id && bc.ContinentCode == code);
                if (!exists)
                    _db.BirdContinents.Add(new BirdContinent { Bird = bird, ContinentCode = code });
            }

            await Task.Delay(100);
        }
    }
}
