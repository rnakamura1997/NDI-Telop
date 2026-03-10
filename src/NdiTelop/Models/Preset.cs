namespace NdiTelop.Models;

public class Preset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<TextLine> TextLines { get; set; } = [];
    public BackgroundStyle Background { get; set; } = new();
    public List<OverlayItem> Overlays { get; set; } = [];
    public AnimationConfig Animation { get; set; } = new();
    public int AutoClearSeconds { get; set; }
}
