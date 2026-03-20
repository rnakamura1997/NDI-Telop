using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace NdiTelop.Models;

public partial class KeyerSlot : ObservableObject
{
    public KeyerDestination Destination { get; set; }

    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _keyOn;

    [ObservableProperty]
    private double _opacity = 1.0;

    [ObservableProperty]
    private int _priority;

    [ObservableProperty]
    private AnimationConfig _animation = new();

    [ObservableProperty]
    private bool _isTransitioning;

    public ObservableCollection<TextBlock> TextBlocks { get; set; } = [];

    public ObservableCollection<OverlayItem> Overlays { get; set; } = [];

    public KeyerBusType BusType => Destination.ToBusType();
}
