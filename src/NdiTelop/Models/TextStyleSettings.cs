using CommunityToolkit.Mvvm.ComponentModel;

namespace NdiTelop.Models;

public partial class TextStyleSettings : ObservableObject
{
    [ObservableProperty]
    private string _fontFamily = string.Empty;

    [ObservableProperty]
    private int _fontSize;

    [ObservableProperty]
    private string _color = string.Empty;

    [ObservableProperty]
    private float _outlineThickness;

    [ObservableProperty]
    private string _outlineColor = "#000000";

    [ObservableProperty]
    private float _shadowOffsetX;

    [ObservableProperty]
    private float _shadowOffsetY;

    [ObservableProperty]
    private float _shadowBlur;

    [ObservableProperty]
    private string _shadowColor = "#00000000";
}
