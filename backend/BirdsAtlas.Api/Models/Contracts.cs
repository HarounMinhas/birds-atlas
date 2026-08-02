namespace BirdsAtlas.Api.Models;

public sealed record BirdListResponse(
    int Page,
    int PageSize,
    long Total,
    bool IsEstimate,
    IReadOnlyList<BirdSummary> Items);

public sealed record BirdSummary(
    long Id,
    string CommonName,
    string EnglishName,
    string ScientificName,
    string Family,
    string Order,
    string Genus,
    string? ImageUrl,
    string? PhotoAttribution,
    string? PhotoLicense,
    string IucnStatus,
    long ObservationCount,
    long? GbifKey);

public sealed record BirdDetail(
    long Id,
    string CommonName,
    string EnglishName,
    string ScientificName,
    string Family,
    string Order,
    string Genus,
    string? ImageUrl,
    string? PhotoAttribution,
    string? PhotoLicense,
    string IucnStatus,
    long ObservationCount,
    long? GbifKey,
    string? WikipediaSummary,
    string? WikipediaUrl,
    string InaturalistUrl,
    string? GbifUrl,
    IReadOnlyList<string> Continents,
    BirdTaxonomy Taxonomy,
    IReadOnlyList<AudioRecording> Recordings);

public sealed record BirdTaxonomy(
    string Kingdom,
    string Phylum,
    string ClassName,
    string Order,
    string Family,
    string Genus,
    string Species,
    long? GbifKey);

public sealed record AudioRecording(
    string Id,
    string CommonName,
    string ScientificName,
    string Recordist,
    string Type,
    string Country,
    string Length,
    string FileUrl,
    string LicenseUrl,
    string SourceUrl);

public sealed record OccurrencePoint(
    long Key,
    double Latitude,
    double Longitude,
    string Country,
    string Locality,
    DateTimeOffset? EventDate,
    string SourceUrl);

public sealed record TaxonomyOptions(
    IReadOnlyList<string> Orders,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Genera);

internal sealed record InatTaxon(
    long Id,
    string ScientificName,
    string CommonName,
    string EnglishName,
    string? ImageUrl,
    string? PhotoAttribution,
    string? PhotoLicense,
    string IucnStatus,
    long ObservationCount,
    string? WikipediaUrl);

internal sealed record GbifMatch(
    long? UsageKey,
    string Kingdom,
    string Phylum,
    string ClassName,
    string Order,
    string Family,
    string Genus,
    string Species);
