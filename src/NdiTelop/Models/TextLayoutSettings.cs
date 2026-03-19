using CommunityToolkit.Mvvm.ComponentModel;

namespace NdiTelop.Models;

public enum HorizontalTextAlignment
{
    Left,
    Center,
    Right
}

public enum VerticalTextAlignment
{
    Top,
    Center,
    Bottom
}

public partial class TextLayoutSettings : ObservableObject
{
    [ObservableProperty]
    private HorizontalTextAlignment _horizontalAlignment = HorizontalTextAlignment.Center;

    [ObservableProperty]
    private VerticalTextAlignment _verticalAlignment = VerticalTextAlignment.Center;

    [ObservableProperty]
    private float _offsetX;

    [ObservableProperty]
    private float _offsetY;
}
