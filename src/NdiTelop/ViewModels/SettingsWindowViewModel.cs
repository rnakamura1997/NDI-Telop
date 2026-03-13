using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using Serilog;

namespace NdiTelop.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly HotkeyService? _hotkeyService;
    private readonly INdiService? _ndiService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private NdiConfig _ndiConfig = new();

    [ObservableProperty]
    private int _webApiPort;

    [ObservableProperty]
    private int _oscPort;

    [ObservableProperty]
    private string _assetPath = string.Empty;

    [ObservableProperty]
    private string _preset1Hotkey = string.Empty;

    [ObservableProperty]
    private string _preset2Hotkey = string.Empty;

    [ObservableProperty]
    private string _preset3Hotkey = string.Empty;

    [ObservableProperty]
    private string _preset4Hotkey = string.Empty;

    [ObservableProperty]
    private string _preset5Hotkey = string.Empty;

    [ObservableProperty]
    private string _clearProgramHotkey = string.Empty;

    public SettingsWindowViewModel(ISettingsService settingsService, HotkeyService? hotkeyService = null, INdiService? ndiService = null)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _ndiService = ndiService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            NdiConfig = CloneNdiConfig(_settingsService.Settings.Ndi);
            WebApiPort = _settingsService.Settings.WebApiPort;
            OscPort = _settingsService.Settings.OscPort;
            AssetPath = _settingsService.Settings.AssetPath;
            Preset1Hotkey = _settingsService.Settings.Hotkeys.Preset1;
            Preset2Hotkey = _settingsService.Settings.Hotkeys.Preset2;
            Preset3Hotkey = _settingsService.Settings.Hotkeys.Preset3;
            Preset4Hotkey = _settingsService.Settings.Hotkeys.Preset4;
            Preset5Hotkey = _settingsService.Settings.Hotkeys.Preset5;
            ClearProgramHotkey = _settingsService.Settings.Hotkeys.ClearProgram;
            Status = "Settings loaded.";
        }
        catch (Exception ex)
        {
            Status = $"Error loading settings: {ex.Message}";
            Log.Error(ex, "Failed to load application settings in SettingsWindow.");
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            _settingsService.Settings.Ndi = CloneNdiConfig(NdiConfig);
            _settingsService.Settings.WebApiPort = WebApiPort;
            _settingsService.Settings.OscPort = OscPort;
            _settingsService.Settings.AssetPath = AssetPath;
            _settingsService.Settings.Hotkeys.Preset1 = Preset1Hotkey;
            _settingsService.Settings.Hotkeys.Preset2 = Preset2Hotkey;
            _settingsService.Settings.Hotkeys.Preset3 = Preset3Hotkey;
            _settingsService.Settings.Hotkeys.Preset4 = Preset4Hotkey;
            _settingsService.Settings.Hotkeys.Preset5 = Preset5Hotkey;
            _settingsService.Settings.Hotkeys.ClearProgram = ClearProgramHotkey;

            await _settingsService.SaveAsync();

            if (_ndiService != null && _ndiService.IsInitialized)
            {
                Status = "NDI更新中...";
                await _ndiService.ReinitializeAsync(_settingsService.Settings.Ndi);
            }

            _hotkeyService?.ApplySettings(_settingsService.Settings.Hotkeys);
            Status = "Settings saved.";
        }
        catch (Exception ex)
        {
            Status = $"Error saving settings: {ex.Message}";
            Log.Error(ex, "Failed to save application settings in SettingsWindow.");
        }
    }

    private static NdiConfig CloneNdiConfig(NdiConfig source)
    {
        return new NdiConfig
        {
            SourceName = source.SourceName,
            ResolutionWidth = source.ResolutionWidth,
            ResolutionHeight = source.ResolutionHeight,
            FrameRateN = source.FrameRateN,
            FrameRateD = source.FrameRateD
        };
    }
}
