using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.Interfaces;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NdiTelop.Utils;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly RenderService _renderService;
    private readonly IPresetService _presetService;
    private readonly INdiService _ndiService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private Preset? _selectedPreset = new() { Name = "New Preset", TextLines = { new TextLine { Text = "Line 1", FontSize = 48, Color = "#FFFFFF" } } };

    partial void OnSelectedPresetChanged(Preset? value)
    {
        // SelectedPreset が null になることはないため、このロジックは不要
    }

    [ObservableProperty]
    private NdiConfig _ndiConfig = new() { SourceName = "NdiTelop", ResolutionWidth = 1920, ResolutionHeight = 1080, FrameRateN = 30000, FrameRateD = 1001 };

    [ObservableProperty]
    private bool _isNdiInitialized;

    public bool CanInitializeNdi => !IsNdiInitialized;

    partial void OnIsNdiInitializedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInitializeNdi));
    }

    [ObservableProperty]
    private bool _isProgramActive;

    [ObservableProperty]
    private bool _isPreviewActive;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    private DispatcherTimer _ndiSendTimer;

    public MainWindowViewModel(RenderService renderService, IPresetService presetService, INdiService ndiService)
    {
        _renderService = renderService;
        _presetService = presetService;
        _ndiService = ndiService;

        _ndiSendTimer = new DispatcherTimer();
        _ndiSendTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (NdiConfig.FrameRateN / NdiConfig.FrameRateD));
        _ndiSendTimer.Tick += NdiSendTimer_Tick;
    }

    [RelayCommand]
    public async Task LoadPresetsAsync()
    {
        await _presetService.LoadPresetsAsync();
        SelectedPreset = Presets.FirstOrDefault();
        Status = $"Loaded {Presets.Count} presets.";
    }

    [RelayCommand]
    public void RenderPreview()
    {
        if (SelectedPreset == null)
        {
            Status = "No preset selected for preview.";
            return;
        }

        try
        {
            // PreviewCanvas will handle rendering based on SelectedPreset
            Status = $"Preview rendered for: {SelectedPreset.Name}";
        }
        catch (Exception ex)
        {
            Status = $"Error rendering preview: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            await _presetService.SavePresetAsync(SelectedPreset);
            Status = $"Preset saved: {SelectedPreset.Name}";
        }
        else
        {
            Status = "No preset selected to save.";
        }
    }

    [RelayCommand]
    public async Task DeleteSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            var presetToDelete = SelectedPreset;
            SelectedPreset = null; // Clear selection before deleting
            await _presetService.DeletePresetAsync(presetToDelete.Id);
            Status = $"Preset deleted: {presetToDelete.Name}";
        }
        else
        {
            Status = "No preset selected to delete.";
        }
    }

    [RelayCommand]
    public async Task InitializeNdiAsync()
    {
        if (_ndiService.IsInitialized) return;

        try
        {
            await _ndiService.InitializeAsync(NdiConfig);
            IsNdiInitialized = _ndiService.IsInitialized;
            Status = "NDI Initialized.";
            _ndiSendTimer.Start();
        }
        catch (Exception ex)
        {
            Status = $"Error initializing NDI: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SetProgramActiveAsync(bool active)
    {
        await _ndiService.SetActiveAsync(NdiChannelType.Program, active);
        IsProgramActive = _ndiService.IsProgramActive;
        Status = $"NDI Program {(active ? "Active" : "Inactive")}.";
    }

    [RelayCommand]
    public async Task SetPreviewActiveAsync(bool active)
    {
        await _ndiService.SetActiveAsync(NdiChannelType.Preview, active);
        IsPreviewActive = _ndiService.IsPreviewActive;
        Status = $"NDI Preview {(active ? "Active" : "Inactive")}.";
    }

    private async void NdiSendTimer_Tick(object? sender, EventArgs e)
    {
        if (SelectedPreset == null || !_ndiService.IsInitialized) return;

        try
        {
            using var bitmap = _renderService.Render(SelectedPreset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);
            await _ndiService.SendFrameAsync(NdiChannelType.Program, bitmap);
            await _ndiService.SendFrameAsync(NdiChannelType.Preview, bitmap);
        }
        catch (Exception ex)
        {
            Status = $"Error sending NDI frame: {ex.Message}";
            _ndiSendTimer.Stop();
        }
    }
}
