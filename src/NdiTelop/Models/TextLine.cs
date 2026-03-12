using CommunityToolkit.Mvvm.ComponentModel;

namespace NdiTelop.Models;

public partial class TextLine : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _fontFamily = "Meiryo";

    [ObservableProperty]
    private int _fontSize = 48;

    [ObservableProperty]
    private string _color = "#FFFFFF";
}
