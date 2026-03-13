using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using Serilog;

namespace NdiTelop.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;

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

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
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
            await _settingsService.SaveAsync();
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
