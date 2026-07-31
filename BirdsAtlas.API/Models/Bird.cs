namespace BirdsAtlas.API.Models;

public class Bird
{
    public int Id { get; set; }
    public string CommonNameEn { get; set; } = string.Empty;
    public string CommonNameNl { get; set; } = string.Empty;
    public string ScientificName { get; set; } = string.Empty;
    public string Order { get; set; } = string.Empty;       // e.g. Passeriformes
    public string Family { get; set; } = string.Empty;      // e.g. Turdidae
    public string Genus { get; set; } = string.Empty;       // e.g. Turdus
    public string Species { get; set; } = string.Empty;     // e.g. merula
    public string? ConservationStatus { get; set; }         // LC, VU, EN, CR
    public string? HabitatType { get; set; }                // forest, wetland, grassland
    public int? GbifTaxonKey { get; set; }
    public int? InatTaxonId { get; set; }
    public string? WikipediaUrl { get; set; }
    public DateTime LastSynced { get; set; } = DateTime.UtcNow;

    public ICollection<BirdCharacteristic> Characteristics { get; set; } = new List<BirdCharacteristic>();
    public ICollection<BirdMedia> Media { get; set; } = new List<BirdMedia>();
    public ICollection<BirdContinent> Continents { get; set; } = new List<BirdContinent>();
}
