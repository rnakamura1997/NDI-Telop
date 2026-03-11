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
using SkiaSharp;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly RenderService _renderService;
    private readonly IPresetService _presetService;
    private readonly INdiService _ndiService;
    private readonly ISettingsService _settingsService;

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

    partial void OnNdiConfigChanged(NdiConfig value)
    {
        if (value.FrameRateN <= 0 || value.FrameRateD <= 0) return;
        _ndiSendTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (value.FrameRateN / (double)value.FrameRateD));
    }

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

    public ObservableCollection<string> AvailableFontFamilies { get; } = new ObservableCollection<string>();

    [ObservableProperty]
    private Preset? _currentProgramPreset;



    private DispatcherTimer? _autoClearTimer;

    private DispatcherTimer? _transitionTimer;
    private Preset? _transitionFromPreset;
    private Preset? _transitionToPreset;
    private float _transitionProgress;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    private DispatcherTimer _ndiSendTimer;



    public MainWindowViewModel(RenderService renderService, IPresetService presetService, INdiService ndiService, ISettingsService settingsService)
    {
        _renderService = renderService;
        _presetService = presetService;
        _ndiService = ndiService;
        _settingsService = settingsService;

        _ndiSendTimer = new DispatcherTimer();
        _ndiSendTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (NdiConfig.FrameRateN / NdiConfig.FrameRateD));
        _ndiSendTimer.Tick += NdiSendTimer_Tick;

        // Load available font families
        foreach (var family in SkiaSharp.SKFontManager.Default.GetFontFamilies())
        {
            AvailableFontFamilies.Add(family);
        }

        // コマンドの初期化
        ShowPresetCommand = new AsyncRelayCommand<Preset>(ShowPresetAsync);

        _autoClearTimer = new DispatcherTimer();
        _autoClearTimer.Interval = TimeSpan.FromSeconds(1);
        _autoClearTimer.Tick += AutoClearTimer_Tick;
    }

    [RelayCommand]
    public async Task LoadPresetsAsync()
    {
        await LoadAppSettingsAsync();
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
    public async Task LoadAppSettingsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            NdiConfig = CloneNdiConfig(_settingsService.Settings.Ndi);
            Status = "App settings loaded.";
        }
        catch (Exception ex)
        {
            Status = $"Error loading app settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task SaveAppSettingsAsync()
    {
        try
        {
            _settingsService.Settings.Ndi = CloneNdiConfig(NdiConfig);
            await _settingsService.SaveAsync();
            Status = "App settings saved.";
        }
        catch (Exception ex)
        {
            Status = $"Error saving app settings: {ex.Message}";
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

    public IAsyncRelayCommand<Preset> ShowPresetCommand { get; }

    private Task ShowPresetAsync(Preset? preset)
    {
        if (preset == null || CurrentProgramPreset == preset) return Task.CompletedTask;

        _transitionFromPreset = CurrentProgramPreset ?? new Preset(); // If null, transition from empty
        _transitionToPreset = preset;
        _transitionProgress = 0f;

        _transitionTimer?.Stop();
        _transitionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Normal, TransitionTimer_Tick);
        _transitionTimer.Start();

        // This will become the new active preset after the transition
        CurrentProgramPreset = preset;

        // AutoClearSeconds の設定
        if (_autoClearTimer == null) return Task.CompletedTask;

        if (preset.AutoClearSeconds > 0)
        {
            _autoClearTimer.Stop();
            _autoClearTimer.Interval = TimeSpan.FromSeconds(preset.AutoClearSeconds);
            _autoClearTimer.Start();
        }
        else
        {
            _autoClearTimer.Stop();
        }

        return Task.CompletedTask;
    }

    private void TransitionTimer_Tick(object? sender, EventArgs e)
    {
        _transitionProgress += 1f / (0.5f * 60); // 0.5 second transition at 60fps

        if (_transitionProgress >= 1f)
        {
            _transitionProgress = 1f;
            _transitionTimer?.Stop();
            _transitionFromPreset = null;
            _transitionToPreset = null;
        }

        // Force redraw of the preview canvas
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private async void AutoClearTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentProgramPreset == null || CurrentProgramPreset.AutoClearSeconds == 0) return;

        // AutoClearSeconds はプリセットごとに設定されるべきだが、ここでは ViewModel のプロパティを使用
        // 実際には CurrentProgramPreset.AutoClearSeconds を使用する
        if (CurrentProgramPreset.AutoClearSeconds > 0 && _ndiService.IsProgramActive)
        {
            // カウンダウンロジック
            // ここでは簡略化のため、タイマーが発火したらすぐにクリアする
            // 実際には経過時間を保持し、AutoClearSeconds に達したらクリアする
            await ClearProgram();
            _autoClearTimer?.Stop();
        }
    }

    [RelayCommand]
    public async Task ClearProgram()
    {
        if (!_ndiService.IsInitialized) return;

        // 透明なフレームを送信してクリア
        var transparentBitmap = new SKBitmap(NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (transparentBitmap)
        {
            using var canvas = new SKCanvas(transparentBitmap);
            canvas.Clear(SKColors.Transparent);
            await _ndiService.SendFrameAsync(NdiChannelType.Program, transparentBitmap);
            await _ndiService.SendFrameAsync(NdiChannelType.Preview, transparentBitmap);
        }
        CurrentProgramPreset = null;
        Status = "Program cleared.";
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

    private async void NdiSendTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentProgramPreset == null || !_ndiService.IsInitialized) return;

        try
        {
            SKBitmap bitmap;
            if (_transitionFromPreset != null && _transitionToPreset != null)
            {
                bitmap = _renderService.RenderTransition(_transitionFromPreset, _transitionToPreset, _transitionProgress, _transitionToPreset.Animation, NdiConfig);
            }
            else
            {
                bitmap = _renderService.Render(CurrentProgramPreset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);
            }

            using (bitmap)
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