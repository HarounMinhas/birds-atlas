namespace BirdsAtlas.API.DTOs;

public record BirdDto(
    int Id,
    string CommonNameEn,
    string CommonNameNl,
    string ScientificName,
    string Order,
    string Family,
    string Genus,
    string? ConservationStatus,
    string? HabitatType,
    string? PrimaryImageUrl,
    List<string> Continents,
    List<CharacteristicDto> Characteristics
);

public record CharacteristicDto(string Key, string Value);

public record BirdListItemDto(
    int Id,
    string CommonNameEn,
    string ScientificName,
    string Order,
    string Family,
    string? ConservationStatus,
    string? PrimaryImageUrl,
    List<string> Continents
);

public record BirdFilterOptions(
    List<string> Continents,
    List<string> Orders,
    List<string> Families,
    List<string> BeakColors,
    List<string> BreastColors,
    List<string> SizeCategories,
    List<string> HabitatTypes,
    List<string> ConservationStatuses
);
