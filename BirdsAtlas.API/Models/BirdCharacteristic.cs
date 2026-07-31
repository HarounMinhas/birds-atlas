namespace BirdsAtlas.API.Models;

public class BirdCharacteristic
{
    public int Id { get; set; }
    public int BirdId { get; set; }
    public Bird Bird { get; set; } = null!;

    // Key examples: beak_color, breast_color, back_color, wing_color,
    //               size_category, beak_shape, tail_shape, leg_color
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;  // e.g. "red", "brown", "large"
    public string? Source { get; set; }                // gbif / inat / manual
}
