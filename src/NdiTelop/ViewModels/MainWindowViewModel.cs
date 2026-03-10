using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Models;
using NdiTelop.Services;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly RenderService _renderService;
    private readonly PresetService _presetService;
    private readonly NdiService _ndiService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private Preset? _selectedPreset;

    [ObservableProperty]
    private Preset? _previewPreset;

    [ObservableProperty]
    private Preset? _programPreset;

    [ObservableProperty]
    private bool _isOnAir;

    [ObservableProperty]
    private bool _isPreviewReady;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    public MainWindowViewModel(RenderService renderService, PresetService presetService)
        : this(renderService, presetService, new NdiService())
    {
    }

    public MainWindowViewModel(RenderService renderService, PresetService presetService, NdiService ndiService)
    {
        _renderService = renderService;
        _presetService = presetService;
        _ndiService = ndiService;
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
    public void RenderPreview() => _ = RefreshPreviewAsync();

    [RelayCommand]
    public async Task RefreshPreviewAsync()
    {
        if (SelectedPreset is null)
        {
            Status = "No preset selected";
            return;
        }

        try
        {
            await _ndiService.InitializeAsync(new NdiConfig());
            using var bmp = _renderService.Render(SelectedPreset, 1280, 720);
            await _ndiService.SendFrameAsync(NdiChannelType.Preview, bmp);
            PreviewPreset = SelectedPreset;
            IsPreviewReady = true;
            Status = $"Preview ready: {SelectedPreset.Name}";
        }
        catch (Exception ex)
        {
            Status = $"Preview failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task PreviewAsync()
    {
        await RefreshPreviewAsync();
    }

    [RelayCommand]
    public async Task TakeAsync()
    {
        try
        {
            await _ndiService.InitializeAsync(new NdiConfig());
            var taken = await _ndiService.TakePreviewToProgramAsync();
            if (!taken || PreviewPreset is null)
            {
                Status = "Take skipped: preview is empty";
                return;
            }

            ProgramPreset = PreviewPreset;
            IsOnAir = true;
            Status = $"On Air: {ProgramPreset.Name}";
        }
        catch (Exception ex)
        {
            Status = $"Take failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task CutAsync()
    {
        try
        {
            await _ndiService.InitializeAsync(new NdiConfig());
            await _ndiService.CutProgramAsync();
            ProgramPreset = null;
            IsOnAir = false;
            Status = "Cut: Program cleared";
        }
        catch (Exception ex)
        {
            Status = $"Cut failed: {ex.Message}";
        }
    }
}
