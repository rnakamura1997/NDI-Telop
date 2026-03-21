using System.Collections.ObjectModel;
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
    private readonly BackupArchiveService? _backupArchiveService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private NdiConfig _ndiConfig = new();

    [ObservableProperty]
    private string _webApiHost = "*";

    [ObservableProperty]
    private int _webApiPort;

    [ObservableProperty]
    private int _oscPort;

    [ObservableProperty]
    private string _oscFeedbackHost = "127.0.0.1";

    [ObservableProperty]
    private int _oscFeedbackPort = 8000;

    [ObservableProperty]
    private bool _enableTallyAutoTake;

    [ObservableProperty]
    private string _tallyPartnerIpAddress = string.Empty;

    [ObservableProperty]
    private string _tallyPartnerName = string.Empty;

    [ObservableProperty]
    private KeyerDestination _tallyAutoTakeKeyer = KeyerDestination.Usk1;

    [ObservableProperty]
    private bool _acceptNdiMetadataTally = true;

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

    public ObservableCollection<HotkeyBindingDisplayItem> HotkeyBindings { get; } = new();
    public IReadOnlyList<KeyerDestination> AvailableKeyers { get; } = KeyerDefinitions.OrderedDestinations;

    public IReadOnlyList<string> AvailableDeckLinkDevices => _outputService?.GetAvailableDeckLinkDevices() ?? [];

    public SettingsWindowViewModel(
        ISettingsService settingsService,
        HotkeyService? hotkeyService = null,
        INdiService? ndiService = null,
        ThemeService? themeService = null,
        IOutputService? outputService = null,
        BackupArchiveService? backupArchiveService = null)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _ndiService = ndiService;
        _themeService = themeService;
        _outputService = outputService;
        _backupArchiveService = backupArchiveService;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            NdiConfig = CloneNdiConfig(_settingsService.Settings.Ndi);
            WebApiHost = _settingsService.Settings.RemoteControl.WebApiHost;
            WebApiPort = _settingsService.Settings.RemoteControl.WebApiPort;
            OscPort = _settingsService.Settings.RemoteControl.OscPort;
            OscFeedbackHost = _settingsService.Settings.RemoteControl.OscFeedbackHost;
            OscFeedbackPort = _settingsService.Settings.RemoteControl.OscFeedbackPort;
            EnableTallyAutoTake = _settingsService.Settings.RemoteControl.EnableTallyAutoTake;
            TallyPartnerIpAddress = _settingsService.Settings.RemoteControl.TallyPartnerIpAddress;
            TallyPartnerName = _settingsService.Settings.RemoteControl.TallyPartnerName;
            TallyAutoTakeKeyer = _settingsService.Settings.RemoteControl.TallyAutoTakeKeyer;
            AcceptNdiMetadataTally = _settingsService.Settings.RemoteControl.AcceptNdiMetadataTally;
            AssetPath = _settingsService.Settings.AssetPath;
            Preset1Hotkey = _settingsService.Settings.Hotkeys.Preset1;
            Preset2Hotkey = _settingsService.Settings.Hotkeys.Preset2;
            Preset3Hotkey = _settingsService.Settings.Hotkeys.Preset3;
            Preset4Hotkey = _settingsService.Settings.Hotkeys.Preset4;
            Preset5Hotkey = _settingsService.Settings.Hotkeys.Preset5;
            ClearProgramHotkey = _settingsService.Settings.Hotkeys.ClearProgram;
            RefreshHotkeyBindings(_settingsService.Settings.Hotkeys);
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


    public async Task CreateBackupAsync(string filePath)
    {
        if (_backupArchiveService == null)
        {
            Status = "Backup service is unavailable.";
            return;
        }

        try
        {
            await SaveAsync();
            await _backupArchiveService.CreateBackupAsync(filePath);
            Status = $"Backup created: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Error creating backup: {ex.Message}";
            Log.Error(ex, "Failed to create backup archive. Path={BackupPath}", filePath);
        }
    }

    public async Task RestoreBackupAsync(string filePath)
    {
        if (_backupArchiveService == null)
        {
            Status = "Backup service is unavailable.";
            return;
        }

        try
        {
            await _backupArchiveService.RestoreBackupAsync(filePath);
            _themeService?.ApplyTheme(_settingsService.Settings.Theme);

            if (_outputService != null)
            {
                await _outputService.ApplySettingsAsync(_settingsService.Settings.Output);
            }

            _hotkeyService?.ApplySettings(_settingsService.Settings.Hotkeys);

            if (_ndiService != null && _ndiService.IsInitialized)
            {
                await _ndiService.ReinitializeAsync(_settingsService.Settings.Ndi);
            }

            await LoadAsync();
            Status = $"Backup restored: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            Status = $"Error restoring backup: {ex.Message}";
            Log.Error(ex, "Failed to restore backup archive. Path={BackupPath}", filePath);
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            _settingsService.Settings.Ndi = CloneNdiConfig(NdiConfig);
            _settingsService.Settings.RemoteControl.WebApiHost = WebApiHost;
            _settingsService.Settings.RemoteControl.WebApiPort = WebApiPort;
            _settingsService.Settings.RemoteControl.OscPort = OscPort;
            _settingsService.Settings.RemoteControl.OscFeedbackHost = OscFeedbackHost;
            _settingsService.Settings.RemoteControl.OscFeedbackPort = OscFeedbackPort;
            _settingsService.Settings.RemoteControl.EnableTallyAutoTake = EnableTallyAutoTake;
            _settingsService.Settings.RemoteControl.TallyPartnerIpAddress = TallyPartnerIpAddress;
            _settingsService.Settings.RemoteControl.TallyPartnerName = TallyPartnerName;
            _settingsService.Settings.RemoteControl.TallyAutoTakeKeyer = TallyAutoTakeKeyer;
            _settingsService.Settings.RemoteControl.AcceptNdiMetadataTally = AcceptNdiMetadataTally;
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
            RefreshHotkeyBindings(_settingsService.Settings.Hotkeys);
            Status = "Settings saved.";
        }
        catch (Exception ex)
        {
            Status = $"Error saving settings: {ex.Message}";
            Log.Error(ex, "Failed to save application settings in SettingsWindow.");
        }
    }


    private void RefreshHotkeyBindings(HotkeySettings settings)
    {
        HotkeyBindings.Clear();

        foreach (var item in HotkeyService.CreateDisplayItems(settings, _hotkeyService?.ActiveBindings))
        {
            HotkeyBindings.Add(item);
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
