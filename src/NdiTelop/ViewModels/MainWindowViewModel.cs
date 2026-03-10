using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.ViewModels;

/// <summary>
/// メイン画面 ViewModel（Phase1）。
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IPresetService _presetService;
    private readonly IRenderService _renderService;
    private readonly INdiService _ndiService;

    [ObservableProperty]
    private string _title = "NdiTelop";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private Preset? _selectedPreset;

    [ObservableProperty]
    private bool _isImmediateSendMode = true;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    public MainWindowViewModel(IPresetService presetService, IRenderService renderService, INdiService ndiService)
    {
        _presetService = presetService;
        _renderService = renderService;
        _ndiService = ndiService;

        SendCommand = new AsyncRelayCommand(SendSelectedPresetAsync, () => SelectedPreset is not null);
        PreviewCommand = new AsyncRelayCommand(PreviewSelectedPresetAsync, () => SelectedPreset is not null);
        InitializeCommand = new AsyncRelayCommand(InitializeAsync);
    }

    public IAsyncRelayCommand InitializeCommand { get; }
    public IAsyncRelayCommand SendCommand { get; }
    public IAsyncRelayCommand PreviewCommand { get; }

    partial void OnSelectedPresetChanged(Preset? value)
    {
        SendCommand.NotifyCanExecuteChanged();
        PreviewCommand.NotifyCanExecuteChanged();
    }

    private async Task InitializeAsync()
    {
        await _presetService.LoadPresetsAsync();
        SelectedPreset = _presetService.Presets.FirstOrDefault();

        await _ndiService.InitializeAsync(new NdiConfig());
        await _ndiService.SetActiveAsync(NdiChannelType.Program, true);
        await _ndiService.SetActiveAsync(NdiChannelType.Preview, true);

        OnPropertyChanged(nameof(Presets));
        StatusMessage = $"プリセット数: {_presetService.Presets.Count}";
    }

    private async Task SendSelectedPresetAsync()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        using var bitmap = _renderService.Render(SelectedPreset, 1920, 1080);
        await _ndiService.SendFrameAsync(NdiChannelType.Program, bitmap);
        StatusMessage = $"PGM送出: {SelectedPreset.Name}";
    }

    private async Task PreviewSelectedPresetAsync()
    {
        if (SelectedPreset is null)
        {
            return;
        }

        using var bitmap = _renderService.Render(SelectedPreset, 1920, 1080);
        await _ndiService.SendFrameAsync(NdiChannelType.Preview, bitmap);
        StatusMessage = $"PVW送出: {SelectedPreset.Name}";
    }
}
