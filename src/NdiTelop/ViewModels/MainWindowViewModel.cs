using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Models;
using NdiTelop.Services;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly RenderService _renderService;
    private readonly PresetService _presetService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private Preset? _selectedPreset;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    public MainWindowViewModel(RenderService renderService, PresetService presetService)
    {
        _renderService = renderService;
        _presetService = presetService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        await _presetService.LoadPresetsAsync();
        SelectedPreset = Presets.FirstOrDefault();
        OnPropertyChanged(nameof(Presets));
        Status = $"Loaded {Presets.Count} presets";
    }

    [RelayCommand]
    public void RenderPreview()
    {
        if (SelectedPreset is null)
        {
            Status = "No preset selected";
            return;
        }

        using var bmp = _renderService.Render(SelectedPreset, 1280, 720);
        Status = $"Rendered {bmp.Width}x{bmp.Height}: {SelectedPreset.Name}";
    }
}
