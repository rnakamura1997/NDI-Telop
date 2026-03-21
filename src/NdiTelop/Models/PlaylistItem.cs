using CommunityToolkit.Mvvm.ComponentModel;

namespace NdiTelop.Models;

public partial class PlaylistItem : ObservableObject
{
    public PlaylistItem(Preset preset)
    {
        Preset = preset;
    }

    public Preset Preset { get; }
    public string PresetId => Preset.Id;
    public string PresetName => Preset.Name;

    [ObservableProperty]
    private int _displayDurationSeconds = 5;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private bool _isNext;

    public string DurationLabel => $"{DisplayDurationSeconds}s";

    partial void OnDisplayDurationSecondsChanged(int value)
    {
        if (value < 0)
        {
            DisplayDurationSeconds = 0;
            return;
        }

        OnPropertyChanged(nameof(DurationLabel));
    }

    partial void OnIsCurrentChanged(bool value) => OnPropertyChanged(nameof(PresetName));
    partial void OnIsNextChanged(bool value) => OnPropertyChanged(nameof(PresetName));
}
