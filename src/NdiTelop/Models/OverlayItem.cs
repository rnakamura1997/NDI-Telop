using CommunityToolkit.Mvvm.ComponentModel;

namespace NdiTelop.Models;

public partial class OverlayItem : ObservableObject
{
    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private int _x;

    [ObservableProperty]
    private int _y;

    [ObservableProperty]
    private int _width;

    [ObservableProperty]
    private int _height;

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private bool _isVisible = true;
}
