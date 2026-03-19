using System.Collections.ObjectModel;

namespace NdiTelop.Models;

public class TextBlock
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Text Block";
    public ObservableCollection<TextLine> TextLines { get; set; } = [];
    public TextStyleSettings TextStyle { get; set; } = new();
    public TextLayoutSettings TextLayout { get; set; } = new();
}
