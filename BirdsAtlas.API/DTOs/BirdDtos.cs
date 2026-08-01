namespace BirdsAtlas.API.DTOs;

/// <summary>Query parameters from Angular filter panel</summary>
public record BirdSearchRequest(
    string? Continent,   // EUROPE | AFRICA | ASIA | NORTH_AMERICA | SOUTH_AMERICA | OCEANIA
    string? Order,       // e.g. Passeriformes
    string? Family,
    string? Genus,
    string? Search,      // free-text on name
    int Offset = 0,
    int Limit = 24
);

/// <summary>Bird card shown in list/grid</summary>
public record BirdSummaryDto(
    int GbifKey,
    string CommonName,
    string ScientificName,
    string Order,
    string Family,
    string? ConservationStatus,
    string? ThumbnailUrl,
    List<string> Continents
);

/// <summary>Full bird detail page data</summary>
public record BirdDetailDto(
    int GbifKey,
    int? InatTaxonId,
    string CommonName,
    string ScientificName,
    string Order,
    string Family,
    string Genus,
    string? ConservationStatus,
    string? Description,
    string? WikipediaUrl,
    string? ImageUrl,
    string? ThumbnailUrl,
    List<string> Continents,
    List<CharacteristicDto> Characteristics,
    List<MediaDto> Sounds
);

public record CharacteristicDto(string Key, string Label, string Value);
public record MediaDto(string Url, string Source, string? Attribution, string? License);

public record PagedResult<T>(List<T> Items, int Offset, int Limit, long Total);

/// <summary>Filter dropdown options — built from static data + live GBIF</summary>
public record FilterMetaDto(List<string> Continents, List<string> Orders);
