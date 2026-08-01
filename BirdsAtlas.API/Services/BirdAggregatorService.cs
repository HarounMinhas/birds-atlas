using BirdsAtlas.API.DTOs;

namespace BirdsAtlas.API.Services;

/// <summary>
/// Orchestrates GBIF + iNaturalist + Xeno-canto into unified DTOs.
/// 
/// Design decision: NO local database. Data is fetched live and held in
/// IMemoryCache for 30–60 minutes. This avoids any database setup while
/// still being responsive after the first request for a given query.
/// </summary>
public class BirdAggregatorService
{
    private readonly GbifService     _gbif;
    private readonly InatService     _inat;
    private readonly XenoCantoService _xc;

    public BirdAggregatorService(GbifService gbif, InatService inat, XenoCantoService xc)
        => (_gbif, _inat, _xc) = (gbif, inat, xc);

    // ── List / Search ─────────────────────────────────────────────────────
    public async Task<PagedResult<BirdSummaryDto>> SearchAsync(BirdSearchRequest req)
    {
        var (species, total) = await _gbif.SearchAsync(req);

        // Enrich every result with iNat thumbnail in parallel
        var summaries = await Task.WhenAll(species.Select(async s =>
        {
            var inat = await _inat.GetByNameAsync(s.CanonicalName);

            // Continent filter: if requested, verify via GBIF occurrence
            List<string> continents = [];
            if (!string.IsNullOrWhiteSpace(req.Continent))
            {
                continents = await _gbif.GetContinentsAsync(s.TaxonKey);
                if (!continents.Contains(req.Continent)) return null; // exclude
            }

            return new BirdSummaryDto(
                GbifKey:            s.TaxonKey,
                CommonName:         inat?.PreferredCommonName ?? s.VernacularName ?? s.CanonicalName,
                ScientificName:     s.CanonicalName,
                Order:              s.Order,
                Family:             s.Family,
                ConservationStatus: inat?.ConservationStatus ?? s.ConservationStatus,
                ThumbnailUrl:       inat?.ThumbnailUrl,
                Continents:         continents
            );
        }));

        var items = summaries.OfType<BirdSummaryDto>().ToList();
        return new PagedResult<BirdSummaryDto>(items, req.Offset, req.Limit, total);
    }

    // ── Detail ────────────────────────────────────────────────────────────
    public async Task<BirdDetailDto?> GetDetailAsync(int gbifKey)
    {
        var species = await _gbif.GetSpeciesAsync(gbifKey);
        if (species is null) return null;

        // Fire iNat + continent checks + Xeno-canto in parallel
        var inatTask       = _inat.GetByNameAsync(species.CanonicalName);
        var continentsTask = _gbif.GetContinentsAsync(gbifKey);
        await Task.WhenAll(inatTask, continentsTask);

        var inat      = inatTask.Result;
        var continents = continentsTask.Result;
        var sounds    = await _xc.GetSoundsAsync(species.CanonicalName);

        // Simple characteristic list from available structured data
        var chars = new List<CharacteristicDto>();
        if (inat?.ConservationStatus != null)
            chars.Add(new("conservation", "Beschermingsstatus", inat.ConservationStatus));
        if (!string.IsNullOrEmpty(species.Order))
            chars.Add(new("order",  "Orde",    species.Order));
        if (!string.IsNullOrEmpty(species.Family))
            chars.Add(new("family", "Familie", species.Family));
        if (!string.IsNullOrEmpty(species.Genus))
            chars.Add(new("genus",  "Genus",   species.Genus));

        return new BirdDetailDto(
            GbifKey:            gbifKey,
            InatTaxonId:        inat?.Id,
            CommonName:         inat?.PreferredCommonName ?? species.VernacularName ?? species.CanonicalName,
            ScientificName:     species.CanonicalName,
            Order:              species.Order,
            Family:             species.Family,
            Genus:              species.Genus,
            ConservationStatus: inat?.ConservationStatus ?? species.ConservationStatus,
            Description:        inat?.WikipediaSummary,
            WikipediaUrl:       inat?.WikipediaUrl,
            ImageUrl:           inat?.ImageUrl,
            ThumbnailUrl:       inat?.ThumbnailUrl,
            Continents:         continents,
            Characteristics:    chars,
            Sounds:             sounds
        );
    }

    // ── Filter meta ───────────────────────────────────────────────────────
    public async Task<FilterMetaDto> GetFilterMetaAsync()
    {
        var orders = await _gbif.GetOrdersAsync();
        return new FilterMetaDto(GbifService.AllContinents, orders);
    }
}
