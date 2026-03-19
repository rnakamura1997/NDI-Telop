using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace NdiTelop.Models;

public class Preset
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<TextBlock> TextBlocks { get; set; } = [];
    public BackgroundStyle Background { get; set; } = new();
    public List<OverlayItem> Overlays { get; set; } = [];
    public AnimationConfig Animation { get; set; } = new();
    public int AutoClearSeconds { get; set; }

    // Legacy properties kept for backward-compatible import of older preset files.
    public ObservableCollection<TextLine> TextLines { get; set; } = [];
    public TextStyleSettings TextStyle { get; set; } = new();
    public TextLayoutSettings TextLayout { get; set; } = new();

    [JsonIgnore]
    public TextBlock PrimaryTextBlock => EnsurePrimaryTextBlock();

    public void EnsureTextBlocksInitialized()
    {
        if (TextBlocks.Count == 0)
        {
            TextBlocks.Add(new TextBlock
            {
                Name = "Text Block 1",
                TextLines = TextLines.Count > 0 ? new ObservableCollection<TextLine>(TextLines) : [],
                TextStyle = TextStyle ?? new TextStyleSettings(),
                TextLayout = TextLayout ?? new TextLayoutSettings()
            });
        }
        else
        {
            var index = 1;
            foreach (var block in TextBlocks)
            {
                block.Name = string.IsNullOrWhiteSpace(block.Name) ? $"Text Block {index}" : block.Name;
                block.TextLines ??= [];
                block.TextStyle ??= new TextStyleSettings();
                block.TextLayout ??= new TextLayoutSettings();
                index++;
            }
        }

        var primary = TextBlocks[0];
        TextLines = primary.TextLines;
        TextStyle = primary.TextStyle;
        TextLayout = primary.TextLayout;
    }

    private TextBlock EnsurePrimaryTextBlock()
    {
        EnsureTextBlocksInitialized();
        return TextBlocks[0];
    }
}
