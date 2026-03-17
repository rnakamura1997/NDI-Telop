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
    private readonly ThemeService? _themeService;
    private readonly IOutputService? _outputService;

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

    [ObservableProperty]
    private string _themeMode = "Light";

    [ObservableProperty]
    private string _accentColor = "#FF0A84FF";

    [ObservableProperty]
    private OutputBackendType _selectedOutputBackend = OutputBackendType.Ndi;

    [ObservableProperty]
    private string _spoutSenderName = "NdiTelop-Spout2";

    [ObservableProperty]
    private int _deckLinkDeviceIndex;


    [ObservableProperty]
    private bool _showDebugLogs = true;

    [ObservableProperty]
    private bool _showInformationLogs = true;

    [ObservableProperty]
    private bool _showWarningLogs = true;

    [ObservableProperty]
    private bool _showErrorLogs = true;

    [ObservableProperty]
    private bool _showFatalLogs = true;

    [ObservableProperty]
    private string _logKeyword = string.Empty;

    [ObservableProperty]
    private bool _autoScrollLogs = true;

    public IReadOnlyList<string> AvailableDeckLinkDevices => _outputService?.GetAvailableDeckLinkDevices() ?? [];

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        HotkeyService? hotkeyService = null,
        INdiService? ndiService = null,
        ThemeService? themeService = null,
        IOutputService? outputService = null)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _ndiService = ndiService;
        _themeService = themeService;
        _outputService = outputService;
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
            ThemeMode = NormalizeThemeMode(_settingsService.Settings.Theme.Mode);
            AccentColor = _settingsService.Settings.Theme.AccentColor;
            SelectedOutputBackend = _settingsService.Settings.Output.SelectedBackend;
            SpoutSenderName = _settingsService.Settings.Output.SpoutSenderName;
            DeckLinkDeviceIndex = _settingsService.Settings.Output.DeckLinkDeviceIndex;

            ShowDebugLogs = _settingsService.Settings.LogViewer.ShowDebug;
            ShowInformationLogs = _settingsService.Settings.LogViewer.ShowInformation;
            ShowWarningLogs = _settingsService.Settings.LogViewer.ShowWarning;
            ShowErrorLogs = _settingsService.Settings.LogViewer.ShowError;
            ShowFatalLogs = _settingsService.Settings.LogViewer.ShowFatal;
            LogKeyword = _settingsService.Settings.LogViewer.Keyword;
            AutoScrollLogs = _settingsService.Settings.LogViewer.AutoScroll;

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
            _settingsService.Settings.Theme.Mode = NormalizeThemeMode(ThemeMode);
            _settingsService.Settings.Theme.AccentColor = AccentColor;
            _settingsService.Settings.Output.SelectedBackend = SelectedOutputBackend;
            _settingsService.Settings.Output.SpoutSenderName = SpoutSenderName;
            _settingsService.Settings.Output.DeckLinkDeviceIndex = DeckLinkDeviceIndex;

            _settingsService.Settings.LogViewer.ShowDebug = ShowDebugLogs;
            _settingsService.Settings.LogViewer.ShowInformation = ShowInformationLogs;
            _settingsService.Settings.LogViewer.ShowWarning = ShowWarningLogs;
            _settingsService.Settings.LogViewer.ShowError = ShowErrorLogs;
            _settingsService.Settings.LogViewer.ShowFatal = ShowFatalLogs;
            _settingsService.Settings.LogViewer.Keyword = LogKeyword;
            _settingsService.Settings.LogViewer.AutoScroll = AutoScrollLogs;

            _themeService?.ApplyTheme(_settingsService.Settings.Theme);

            await _settingsService.SaveAsync();

            if (_ndiService != null && _ndiService.IsInitialized)
            {
                Status = "NDI更新中...";
                await _ndiService.ReinitializeAsync(_settingsService.Settings.Ndi);
            }

            if (_outputService != null)
            {
                await _outputService.ApplySettingsAsync(_settingsService.Settings.Output);
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


    private static string NormalizeThemeMode(string? mode)
    {
        return mode?.Trim().ToLowerInvariant() == "dark" ? "Dark" : "Light";
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
