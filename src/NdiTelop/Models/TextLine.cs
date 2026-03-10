namespace NdiTelop.Models;

/// <summary>
/// 1行分のテロップ設定。
/// </summary>
public class TextLine
{
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Meiryo";
    public int FontSize { get; set; } = 48;
    public string Color { get; set; } = "#FFFFFF";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public float OutlineWidth { get; set; }
    public string OutlineColor { get; set; } = "#000000";
    public TextAlignmentType Alignment { get; set; } = TextAlignmentType.Center;
}
