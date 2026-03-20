using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace NdiTelop.Models;

public class Preset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public BackgroundStyle Background { get; set; } = new();
    public ObservableCollection<KeyerSlot> UskKeyers { get; set; } = [];
    public ObservableCollection<KeyerSlot> DskKeyers { get; set; } = [];
    public AnimationConfig Animation { get; set; } = new();
    public int AutoClearSeconds { get; set; }

    // Legacy import properties.
    public ObservableCollection<TextBlock> TextBlocks { get; set; } = [];
    public ObservableCollection<OverlayItem> Overlays { get; set; } = [];

    // Legacy properties kept for backward-compatible import of older preset files.
    public ObservableCollection<TextLine> TextLines { get; set; } = [];
    public TextStyleSettings TextStyle { get; set; } = new();
    public TextLayoutSettings TextLayout { get; set; } = new();

    [JsonIgnore]
    public TextBlock PrimaryTextBlock => EnsurePrimaryTextBlock();

    [JsonIgnore]
    public IEnumerable<KeyerSlot> Keyers => UskKeyers.Concat(DskKeyers);

    public void EnsureTextBlocksInitialized()
    {
        EnsureKeyersInitialized();

        if (TextBlocks.Count > 0)
        {
            var usk1 = GetKeyer(KeyerDestination.Usk1);
            foreach (var block in TextBlocks.Where(block => !usk1.TextBlocks.Contains(block)))
            {
                block.DestinationKeyer = KeyerDestination.Usk1;
                usk1.TextBlocks.Add(block);
            }

            TextBlocks.Clear();
        }

        if (Overlays.Count > 0)
        {
            var usk1 = GetKeyer(KeyerDestination.Usk1);
            foreach (var overlay in Overlays.Where(overlay => !usk1.Overlays.Contains(overlay)))
            {
                overlay.DestinationKeyer = KeyerDestination.Usk1;
                usk1.Overlays.Add(overlay);
            }

            Overlays.Clear();
        }

        if (!GetAllTextBlocks().Any())
        {
            GetKeyer(KeyerDestination.Usk1).TextBlocks.Add(new TextBlock
            {
                Name = "Text Block 1",
                DestinationKeyer = KeyerDestination.Usk1,
                TextLines = TextLines.Count > 0 ? new ObservableCollection<TextLine>(TextLines) : [],
                TextStyle = TextStyle ?? new TextStyleSettings(),
                TextLayout = TextLayout ?? new TextLayoutSettings()
            });
        }

        var index = 1;
        foreach (var block in GetAllTextBlocks())
        {
            block.Name = string.IsNullOrWhiteSpace(block.Name) ? $"Text Block {index}" : block.Name;
            block.TextLines ??= [];
            block.TextStyle ??= new TextStyleSettings();
            block.TextLayout ??= new TextLayoutSettings();
            index++;
        }

        var primary = GetAllTextBlocks().First();
        TextLines = primary.TextLines;
        TextStyle = primary.TextStyle;
        TextLayout = primary.TextLayout;
    }

    public void EnsureKeyersInitialized()
    {
        EnsureKeyerCollection(UskKeyers, KeyerBusType.Usk);
        EnsureKeyerCollection(DskKeyers, KeyerBusType.Dsk);
    }

    public KeyerSlot GetKeyer(KeyerDestination destination)
    {
        EnsureKeyersInitialized();
        return Keyers.First(keyer => keyer.Destination == destination);
    }

    public IReadOnlyList<TextBlock> GetAllTextBlocks()
        => Keyers.SelectMany(keyer => keyer.TextBlocks).ToList();

    public IReadOnlyList<OverlayItem> GetAllOverlays()
        => Keyers.SelectMany(keyer => keyer.Overlays).ToList();

    private TextBlock EnsurePrimaryTextBlock()
    {
        EnsureTextBlocksInitialized();
        return GetAllTextBlocks().First();
    }

    private static void EnsureKeyerCollection(ObservableCollection<KeyerSlot> keyers, KeyerBusType busType)
    {
        var expected = busType == KeyerBusType.Usk
            ? KeyerDefinitions.OrderedDestinations.Where(x => x.ToBusType() == KeyerBusType.Usk)
            : KeyerDefinitions.OrderedDestinations.Where(x => x.ToBusType() == KeyerBusType.Dsk);

        foreach (var destination in expected)
        {
            var existing = keyers.FirstOrDefault(keyer => keyer.Destination == destination);
            if (existing == null)
            {
                keyers.Add(new KeyerSlot
                {
                    Destination = destination,
                    Name = destination.ToDisplayName(),
                    KeyOn = destination == KeyerDestination.Usk1,
                    Opacity = 1.0,
                    Priority = destination.ToDefaultPriority(),
                    Animation = new AnimationConfig()
                });
                continue;
            }

            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? destination.ToDisplayName() : existing.Name;
            existing.Opacity = Math.Clamp(existing.Opacity, 0.0, 1.0);
            existing.Priority = existing.Priority == 0 ? destination.ToDefaultPriority() : existing.Priority;
            existing.Animation ??= new AnimationConfig();
        }
    }
}
