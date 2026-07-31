namespace BirdsAtlas.API.DTOs;

public record BirdFilterRequest(
    string? Continent,           // EUROPE | AFRICA | ASIA | NORTH_AMERICA | SOUTH_AMERICA | OCEANIA
    string? Order,               // e.g. Passeriformes
    string? Family,              // e.g. Turdidae
    string? Genus,
    string? BeakColor,
    string? BreastColor,
    string? BackColor,
    string? SizeCategory,        // small | medium | large
    string? HabitatType,         // forest | wetland | grassland | coastal
    string? ConservationStatus,  // LC | VU | EN | CR
    string? Search,              // free text on name
    int Page = 1,
    int PageSize = 24
);
