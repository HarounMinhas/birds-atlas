namespace BirdsAtlas.API.Models;

// Many-to-many: bird ↔ continents
public class BirdContinent
{
    public int BirdId { get; set; }
    public Bird Bird { get; set; } = null!;
    // EUROPE, AFRICA, ASIA, NORTH_AMERICA, SOUTH_AMERICA, OCEANIA, ANTARCTICA
    public string ContinentCode { get; set; } = string.Empty;
}
