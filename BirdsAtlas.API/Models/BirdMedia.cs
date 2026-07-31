namespace BirdsAtlas.API.Models;

public enum MediaType { Image, Audio }

public class BirdMedia
{
    public int Id { get; set; }
    public int BirdId { get; set; }
    public Bird Bird { get; set; } = null!;
    public MediaType Type { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ThumbnailUrl { get; set; }
    public string Source { get; set; } = string.Empty; // "inat" | "xeno-canto"
    public bool IsPrimary { get; set; } = false;
    public string? License { get; set; }
    public string? Attribution { get; set; }
}
