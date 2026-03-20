using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NdiTelop.Models;

public partial class TextBlock : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [ObservableProperty]
    private string _name = "Text Block";

    [ObservableProperty]
    private KeyerDestination _destinationKeyer = KeyerDestination.Usk1;

    public ObservableCollection<TextLine> TextLines { get; set; } = [];
    public TextStyleSettings TextStyle { get; set; } = new();
    public TextLayoutSettings TextLayout { get; set; } = new();
}
