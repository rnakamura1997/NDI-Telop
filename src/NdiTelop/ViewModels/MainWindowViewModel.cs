using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.Interfaces;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using NdiTelop.Utils;
using SkiaSharp;
using System.ComponentModel;
using NdiTelop.Logging;
using Serilog;
using Serilog.Events;
using System.IO;

namespace NdiTelop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static Preset CreateDefaultPreset()
    {
        var preset = new Preset { Name = "New Preset" };
        preset.GetKeyer(KeyerDestination.Usk1).TextBlocks.Add(new Models.TextBlock
        {
            Name = "Text Block 1",
            DestinationKeyer = KeyerDestination.Usk1,
            TextStyle = new TextStyleSettings { FontSize = 48, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings(),
            TextLines = [new TextLine { Text = "Line 1", FontSize = 48, Color = "#FFFFFF" }]
        });
        preset.EnsureTextBlocksInitialized();
        return preset;
    }

    private readonly RenderService _renderService;
    private readonly IPresetService _presetService;
    private readonly INdiService _ndiService;
    private readonly ISettingsService _settingsService;
    private readonly ExternalControlCoordinator? _externalControlCoordinator;
    private AssetService _assetService;
    private readonly HotkeyService? _hotkeyService;

    [ObservableProperty]
    private string _status = "Ready";

    [ObservableProperty]
    private string _ndiOutputStatus = "Inactive";

    [ObservableProperty]
    private string _ndiOutputStatusColor = "#888888";

    private bool _hasNdiError;


    [ObservableProperty]
    private Preset? _selectedPreset = CreateDefaultPreset();

    [ObservableProperty]
    private Models.TextBlock? _selectedTextBlock;

    partial void OnSelectedPresetChanged(Preset? value)
    {
        value?.EnsureTextBlocksInitialized();
        AttachOverlayListeners(value);
        AttachTextBlockListeners(value);
        RefreshEditorCollections(value);
        SelectedTextBlock = EditableTextBlocks.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedPreset));
    }

    partial void OnSelectedTextBlockChanged(Models.TextBlock? value)
    {
        AttachTextLineListeners(value);
        AttachTextStyleListeners(value);
        AttachTextLayoutListeners(value);
        OnPropertyChanged(nameof(SelectedTextBlock));
        OnPropertyChanged(nameof(SelectedPreset));
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
    public ObservableCollection<RecentLogEntry> FilteredLogs { get; } = [];
    public ObservableCollection<Preset> FilteredPresets { get; } = [];
    public ObservableCollection<AssetItem> AssetItems { get; } = new();
    public ObservableCollection<Models.TextBlock> EditableTextBlocks { get; } = [];
    public ObservableCollection<OverlayItem> EditableOverlays { get; } = [];
    public ObservableCollection<KeyerSlot> UskKeyers { get; } = [];
    public ObservableCollection<KeyerSlot> DskKeyers { get; } = [];
    public ObservableCollection<KeyerDestination> AvailableDestinationKeyers { get; } = new(KeyerDefinitions.OrderedDestinations);
    public ObservableCollection<int> AvailableKeyerPriorities { get; } = [4, 3, 2, 1];

    [ObservableProperty]
    private AssetItem? _selectedAsset;

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
    private string _presetSearchKeyword = string.Empty;

    [ObservableProperty]
    private bool _autoScrollLogs = true;

    [ObservableProperty]
    private bool _shouldScrollLogsToEnd;


    public ObservableCollection<HorizontalTextAlignment> AvailableHorizontalAlignments { get; } =
    [
        HorizontalTextAlignment.Left,
        HorizontalTextAlignment.Center,
        HorizontalTextAlignment.Right
    ];

    public ObservableCollection<VerticalTextAlignment> AvailableVerticalAlignments { get; } =
    [
        VerticalTextAlignment.Top,
        VerticalTextAlignment.Center,
        VerticalTextAlignment.Bottom
    ];

    public ObservableCollection<SelectionAlignmentReferenceMode> AvailableAlignmentReferenceModes { get; } =
    [
        SelectionAlignmentReferenceMode.SelectionBounds,
        SelectionAlignmentReferenceMode.LastSelectedElement
    ];

    [ObservableProperty]
    private SelectionAlignmentReferenceMode _selectedAlignmentReferenceMode = SelectionAlignmentReferenceMode.SelectionBounds;

    public ObservableCollection<string> AvailableTransitionTypes { get; } = new ObservableCollection<string>
    {
        "fade",
        "slide",
        "wipe",
        "wipe-vertical",
        "zoom"
    };

    [ObservableProperty]
    private Preset? _currentProgramPreset;

    [ObservableProperty]
    private Preset? _currentPreviewPreset;

    [ObservableProperty]
    private int _autoClearRemainingSeconds;

    public string AutoClearStatusText => AutoClearRemainingSeconds > 0
        ? $"AutoClear in {AutoClearRemainingSeconds}s"
        : "AutoClear inactive";




    private Preset? _overlayBoundPreset;
    private readonly List<KeyerSlot> _overlayBoundKeyers = [];

    private void AttachOverlayListeners(Preset? preset)
    {
        if (_overlayBoundPreset != null)
        {
            foreach (var keyer in _overlayBoundKeyers)
            {
                keyer.PropertyChanged -= KeyerSlot_PropertyChanged;
                keyer.Overlays.CollectionChanged -= Overlays_CollectionChanged;
                foreach (var overlay in keyer.Overlays)
                {
                    overlay.PropertyChanged -= OverlayItem_PropertyChanged;
                }
            }
        }

        _overlayBoundPreset = preset;
        _overlayBoundKeyers.Clear();

        if (_overlayBoundPreset == null)
        {
            return;
        }

        _overlayBoundPreset.EnsureKeyersInitialized();
        foreach (var keyer in _overlayBoundPreset.Keyers)
        {
            _overlayBoundKeyers.Add(keyer);
            keyer.PropertyChanged += KeyerSlot_PropertyChanged;
            keyer.Overlays.CollectionChanged += Overlays_CollectionChanged;
            foreach (var overlay in keyer.Overlays)
            {
                overlay.PropertyChanged += OverlayItem_PropertyChanged;
            }
        }
    }

    private void Overlays_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var overlay in e.OldItems.OfType<OverlayItem>())
            {
                overlay.PropertyChanged -= OverlayItem_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var overlay in e.NewItems.OfType<OverlayItem>())
            {
                overlay.PropertyChanged += OverlayItem_PropertyChanged;
            }
        }

        RefreshEditorCollections(SelectedPreset);
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private void OverlayItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(OverlayItem.Path)
            or nameof(OverlayItem.X)
            or nameof(OverlayItem.Y)
            or nameof(OverlayItem.Width)
            or nameof(OverlayItem.Height)
            or nameof(OverlayItem.Opacity)
            or nameof(OverlayItem.IsVisible)
            or nameof(OverlayItem.DestinationKeyer))
        {
            if (sender is OverlayItem overlay && e.PropertyName == nameof(OverlayItem.DestinationKeyer))
            {
                MoveOverlayToDestination(overlay);
            }

            OnPropertyChanged(nameof(SelectedPreset));
        }
    }

    private Preset? _textBlockBoundPreset;
    private readonly List<KeyerSlot> _textBlockBoundKeyers = [];

    private void AttachTextBlockListeners(Preset? preset)
    {
        if (_textBlockBoundPreset != null)
        {
            foreach (var keyer in _textBlockBoundKeyers)
            {
                keyer.PropertyChanged -= KeyerSlot_PropertyChanged;
                keyer.TextBlocks.CollectionChanged -= TextBlocks_CollectionChanged;
                foreach (var block in keyer.TextBlocks)
                {
                    block.PropertyChanged -= TextBlock_PropertyChanged;
                    block.TextLines.CollectionChanged -= TextLines_CollectionChanged;
                    block.TextStyle.PropertyChanged -= TextStyle_PropertyChanged;
                    block.TextLayout.PropertyChanged -= TextLayout_PropertyChanged;
                    foreach (var line in block.TextLines)
                    {
                        line.PropertyChanged -= TextLine_PropertyChanged;
                    }
                }
            }
        }

        _textBlockBoundPreset = preset;
        _textBlockBoundKeyers.Clear();

        if (_textBlockBoundPreset == null)
        {
            return;
        }

        _textBlockBoundPreset.EnsureTextBlocksInitialized();
        foreach (var keyer in _textBlockBoundPreset.Keyers)
        {
            _textBlockBoundKeyers.Add(keyer);
            keyer.PropertyChanged += KeyerSlot_PropertyChanged;
            keyer.TextBlocks.CollectionChanged += TextBlocks_CollectionChanged;
            foreach (var block in keyer.TextBlocks)
            {
                block.PropertyChanged += TextBlock_PropertyChanged;
                block.TextLines.CollectionChanged += TextLines_CollectionChanged;
                block.TextStyle.PropertyChanged += TextStyle_PropertyChanged;
                block.TextLayout.PropertyChanged += TextLayout_PropertyChanged;
                foreach (var line in block.TextLines)
                {
                    line.PropertyChanged += TextLine_PropertyChanged;
                }
            }
        }
    }

    private void TextBlocks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var removed in e.OldItems.OfType<Models.TextBlock>())
            {
                removed.PropertyChanged -= TextBlock_PropertyChanged;
                removed.TextLines.CollectionChanged -= TextLines_CollectionChanged;
                removed.TextStyle.PropertyChanged -= TextStyle_PropertyChanged;
                removed.TextLayout.PropertyChanged -= TextLayout_PropertyChanged;
                foreach (var line in removed.TextLines)
                {
                    line.PropertyChanged -= TextLine_PropertyChanged;
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (var added in e.NewItems.OfType<Models.TextBlock>())
            {
                added.PropertyChanged += TextBlock_PropertyChanged;
                added.TextLines.CollectionChanged += TextLines_CollectionChanged;
                added.TextStyle.PropertyChanged += TextStyle_PropertyChanged;
                added.TextLayout.PropertyChanged += TextLayout_PropertyChanged;
                foreach (var line in added.TextLines)
                {
                    line.PropertyChanged += TextLine_PropertyChanged;
                }
            }
        }

        RefreshEditorCollections(SelectedPreset);
        if (SelectedTextBlock != null && !EditableTextBlocks.Contains(SelectedTextBlock))
        {
            SelectedTextBlock = EditableTextBlocks.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedPreset));
    }

    private void TextBlock_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Models.TextBlock.DestinationKeyer) && sender is Models.TextBlock block)
        {
            MoveTextBlockToDestination(block);
        }

        OnPropertyChanged(nameof(SelectedPreset));
    }

    private void KeyerSlot_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshEditorCollections(SelectedPreset);
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private Models.TextBlock? _textLineBoundBlock;

    private void AttachTextLineListeners(Models.TextBlock? block)
    {
        if (_textLineBoundBlock != null)
        {
            _textLineBoundBlock.TextLines.CollectionChanged -= TextLines_CollectionChanged;
            foreach (var line in _textLineBoundBlock.TextLines)
            {
                line.PropertyChanged -= TextLine_PropertyChanged;
            }
        }

        _textLineBoundBlock = block;

        if (_textLineBoundBlock == null)
        {
            return;
        }

        _textLineBoundBlock.TextLines.CollectionChanged += TextLines_CollectionChanged;
        foreach (var line in _textLineBoundBlock.TextLines)
        {
            line.PropertyChanged += TextLine_PropertyChanged;
        }
    }

    private void TextLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var removed in e.OldItems.OfType<TextLine>())
            {
                removed.PropertyChanged -= TextLine_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var added in e.NewItems.OfType<TextLine>())
            {
                added.PropertyChanged += TextLine_PropertyChanged;
            }
        }

        OnPropertyChanged(nameof(SelectedPreset));
    }

    private void TextLine_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private Models.TextBlock? _textLayoutBoundBlock;

    private void AttachTextLayoutListeners(Models.TextBlock? block)
    {
        if (_textLayoutBoundBlock?.TextLayout != null)
        {
            _textLayoutBoundBlock.TextLayout.PropertyChanged -= TextLayout_PropertyChanged;
        }

        _textLayoutBoundBlock = block;

        if (_textLayoutBoundBlock?.TextLayout == null)
        {
            return;
        }

        _textLayoutBoundBlock.TextLayout.PropertyChanged += TextLayout_PropertyChanged;
    }

    private void TextLayout_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private Models.TextBlock? _textStyleBoundBlock;

    private void AttachTextStyleListeners(Models.TextBlock? block)
    {
        if (_textStyleBoundBlock?.TextStyle != null)
        {
            _textStyleBoundBlock.TextStyle.PropertyChanged -= TextStyle_PropertyChanged;
        }

        _textStyleBoundBlock = block;

        if (_textStyleBoundBlock?.TextStyle == null)
        {
            return;
        }

        _textStyleBoundBlock.TextStyle.PropertyChanged += TextStyle_PropertyChanged;
    }

    private void TextStyle_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private DispatcherTimer? _autoClearTimer;
    private bool _autoClearEnabled;

    private DispatcherTimer? _transitionTimer;
    private Preset? _transitionFromPreset;
    private Preset? _transitionToPreset;
    private float _transitionProgress;

    public IReadOnlyList<Preset> Presets => _presetService.Presets;

    private DispatcherTimer _ndiSendTimer;



    public MainWindowViewModel(RenderService renderService, IPresetService presetService, INdiService ndiService, ISettingsService settingsService, ExternalControlCoordinator? externalControlCoordinator = null, AssetService? assetService = null, HotkeyService? hotkeyService = null)
    {
        _renderService = renderService;
        _presetService = presetService;
        _ndiService = ndiService;
        _settingsService = settingsService;
        _externalControlCoordinator = externalControlCoordinator;
        _assetService = assetService ?? new AssetService();
        _assetService.AssetsChanged += AssetService_AssetsChanged;
        _hotkeyService = hotkeyService;

        _ndiSendTimer = new DispatcherTimer();
        _ndiSendTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / (NdiConfig.FrameRateN / NdiConfig.FrameRateD));
        _ndiSendTimer.Tick += NdiSendTimer_Tick;

        // Load available font families
        foreach (var family in SkiaSharp.SKFontManager.Default.GetFontFamilies())
        {
            AvailableFontFamilies.Add(family);
        }

        EnsureTextStyleDefaults(SelectedPreset);
        AttachOverlayListeners(SelectedPreset);
        AttachTextBlockListeners(SelectedPreset);
        RefreshEditorCollections(SelectedPreset);
        SelectedTextBlock = EditableTextBlocks.FirstOrDefault();

        // コマンドの初期化
        ShowPresetCommand = new AsyncRelayCommand<Preset>(ShowPresetAsync);

        _autoClearTimer = new DispatcherTimer();
        _autoClearTimer.Interval = TimeSpan.FromSeconds(1);
        _autoClearTimer.Tick += AutoClearTimer_Tick;

        if (_externalControlCoordinator != null)
        {
            _externalControlCoordinator.ShowPresetHandler = preset => ShowPresetAsync(preset);
            _externalControlCoordinator.ClearProgramHandler = ClearProgram;
            _externalControlCoordinator.GetNdiOutputStatusHandler = () => NdiOutputStatus;
            _externalControlCoordinator.GetBasicSettingsHandler = CreateExternalBasicSettings;
        }

        if (_hotkeyService != null)
        {
            _hotkeyService.HotkeyPressed += HandleHotkeyPressed;
        }

        AppLogger.InMemorySink.RecentLogs.CollectionChanged += (_, _) => RefreshFilteredLogs();
        RefreshFilteredLogs();

        if (_presetService.Presets is INotifyCollectionChanged presetCollection)
        {
            presetCollection.CollectionChanged += Presets_CollectionChanged;
        }

        RefreshFilteredPresets();

        RefreshNdiOutputStatus("初期状態");
    }

    partial void OnShowDebugLogsChanged(bool value) => RefreshFilteredLogs();
    partial void OnShowInformationLogsChanged(bool value) => RefreshFilteredLogs();
    partial void OnShowWarningLogsChanged(bool value) => RefreshFilteredLogs();
    partial void OnShowErrorLogsChanged(bool value) => RefreshFilteredLogs();
    partial void OnShowFatalLogsChanged(bool value) => RefreshFilteredLogs();
    partial void OnLogKeywordChanged(string value) => RefreshFilteredLogs();
    partial void OnPresetSearchKeywordChanged(string value) => RefreshFilteredPresets();

    [RelayCommand]
    private void ClearLogKeyword()
    {
        LogKeyword = string.Empty;
    }

    [RelayCommand]
    private void ClearPresetSearch()
    {
        PresetSearchKeyword = string.Empty;
    }

    [RelayCommand]
    private async Task ExportVisibleLogsAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            var content = string.Join(Environment.NewLine, FilteredLogs.Select(log => log.Formatted));
            await File.WriteAllTextAsync(filePath, content);
            Status = $"Logs exported: {filePath}";
            Log.Information("Filtered logs exported. Path={Path}, Count={Count}", filePath, FilteredLogs.Count);
        }
        catch (Exception ex)
        {
            Status = $"Log export failed: {ex.Message}";
            Log.Error(ex, "Failed to export filtered logs.");
        }
    }

    private void RefreshFilteredLogs()
    {
        var keyword = LogKeyword?.Trim();

        var logs = AppLogger.InMemorySink.RecentLogs.Where(log =>
            IsLevelVisible(log.Level) &&
            (string.IsNullOrEmpty(keyword) || log.Message.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

        FilteredLogs.Clear();
        foreach (var entry in logs)
        {
            FilteredLogs.Add(entry);
        }

        if (AutoScrollLogs)
        {
            ShouldScrollLogsToEnd = !ShouldScrollLogsToEnd;
        }
    }

    private void Presets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshFilteredPresets();
    }

    private void RefreshFilteredPresets()
    {
        var keyword = PresetSearchKeyword?.Trim();
        var filtered = Presets.Where(p => string.IsNullOrWhiteSpace(keyword) || p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));

        FilteredPresets.Clear();
        foreach (var preset in filtered)
        {
            FilteredPresets.Add(preset);
        }

        if (SelectedPreset != null && !FilteredPresets.Contains(SelectedPreset) && Presets.Count > 0)
        {
            SelectedPreset = FilteredPresets.FirstOrDefault();
        }
        else if (SelectedPreset == null && FilteredPresets.Count > 0)
        {
            SelectedPreset = FilteredPresets[0];
        }
    }

    private bool IsLevelVisible(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Debug => ShowDebugLogs,
            LogEventLevel.Information => ShowInformationLogs,
            LogEventLevel.Warning => ShowWarningLogs,
            LogEventLevel.Error => ShowErrorLogs,
            LogEventLevel.Fatal => ShowFatalLogs,
            _ => true
        };
    }

    private void RefreshEditorCollections(Preset? preset)
    {
        EditableTextBlocks.Clear();
        EditableOverlays.Clear();
        UskKeyers.Clear();
        DskKeyers.Clear();

        if (preset == null)
        {
            return;
        }

        preset.EnsureTextBlocksInitialized();
        foreach (var keyer in preset.UskKeyers)
        {
            UskKeyers.Add(keyer);
        }

        foreach (var keyer in preset.DskKeyers)
        {
            DskKeyers.Add(keyer);
        }

        foreach (var block in preset.GetAllTextBlocks())
        {
            EditableTextBlocks.Add(block);
        }

        foreach (var overlay in preset.GetAllOverlays())
        {
            EditableOverlays.Add(overlay);
        }
    }

    private void MoveTextBlockToDestination(Models.TextBlock block)
    {
        if (SelectedPreset == null)
        {
            return;
        }

        foreach (var keyer in SelectedPreset.Keyers)
        {
            if (keyer.Destination == block.DestinationKeyer)
            {
                if (!keyer.TextBlocks.Contains(block))
                {
                    keyer.TextBlocks.Add(block);
                }
            }
            else if (keyer.TextBlocks.Contains(block))
            {
                keyer.TextBlocks.Remove(block);
            }
        }

        RefreshEditorCollections(SelectedPreset);
    }

    private void MoveOverlayToDestination(OverlayItem overlay)
    {
        if (SelectedPreset == null)
        {
            return;
        }

        foreach (var keyer in SelectedPreset.Keyers)
        {
            if (keyer.Destination == overlay.DestinationKeyer)
            {
                if (!keyer.Overlays.Contains(overlay))
                {
                    keyer.Overlays.Add(overlay);
                }
            }
            else if (keyer.Overlays.Contains(overlay))
            {
                keyer.Overlays.Remove(overlay);
            }
        }

        RefreshEditorCollections(SelectedPreset);
    }

    [RelayCommand]
    public async Task LoadPresetsAsync()
    {
        await LoadAppSettingsAsync();
        await _presetService.LoadPresetsAsync();
        _hotkeyService?.ApplySettings(_settingsService.Settings.Hotkeys);
        foreach (var preset in Presets)
        {
            EnsureTextStyleDefaults(preset);
        }

        RefreshFilteredPresets();
        SelectedPreset ??= FilteredPresets.FirstOrDefault();
        RefreshEditorCollections(SelectedPreset);
        Status = $"Loaded {Presets.Count} presets.";
        Log.Information("Loaded presets count: {Count}", Presets.Count);
    }

    private async void HandleHotkeyPressed(HotkeyAction action)
    {
        var preset = action switch
        {
            HotkeyAction.Preset1 => Presets.ElementAtOrDefault(0),
            HotkeyAction.Preset2 => Presets.ElementAtOrDefault(1),
            HotkeyAction.Preset3 => Presets.ElementAtOrDefault(2),
            HotkeyAction.Preset4 => Presets.ElementAtOrDefault(3),
            HotkeyAction.Preset5 => Presets.ElementAtOrDefault(4),
            _ => null
        };

        if (action == HotkeyAction.ClearProgram)
        {
            await ClearProgram();
            return;
        }

        if (preset != null)
        {
            await ShowPresetAsync(preset);
            Status = $"Hotkey: {preset.Name}";
        }
    }

    public Task TriggerPresetByNumberAsync(int number)
    {
        if (number is < 1 or > 9)
        {
            Status = $"NumPad{number} ignored: unsupported key.";
            return Task.CompletedTask;
        }

        var preset = Presets.ElementAtOrDefault(number - 1);
        if (preset == null)
        {
            Status = $"NumPad{number} ignored: no preset assigned.";
            return Task.CompletedTask;
        }

        return TriggerPresetByNumberCoreAsync(number, preset);
    }

    private async Task TriggerPresetByNumberCoreAsync(int number, Preset preset)
    {
        await ShowPresetAsync(preset);
        Status = $"NumPad{number}: {preset.Name}";
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
            Log.Error(ex, "Preview rendering failed.");
        }
    }

    [RelayCommand]
    public async Task SaveSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            await _presetService.SavePresetAsync(SelectedPreset);
            Status = $"Preset saved: {SelectedPreset.Name}";
            Log.Information("Preset saved. Name={PresetName}, Id={PresetId}", SelectedPreset.Name, SelectedPreset.Id);
        }
        else
        {
            Status = "No preset selected to save.";
        }
    }

    public async Task MovePresetAsync(string presetId, int targetIndex)
    {
        await _presetService.MovePresetAsync(presetId, targetIndex);
        OnPropertyChanged(nameof(Presets));
        RefreshFilteredPresets();
        Status = "Preset order updated.";
    }

    [RelayCommand]
    public async Task DuplicateSelectedPresetAsync()
    {
        if (SelectedPreset == null)
        {
            Status = "No preset selected to duplicate.";
            return;
        }

        var sourceId = SelectedPreset.Id;
        var duplicated = await _presetService.DuplicatePresetAsync(sourceId);
        if (duplicated == null)
        {
            Status = "Preset duplication failed.";
            return;
        }

        SelectedPreset = duplicated;
        OnPropertyChanged(nameof(Presets));
        RefreshFilteredPresets();
        Status = $"Preset duplicated: {duplicated.Name}";
        Log.Information("Preset duplicated. SourceId={SourceId}, NewId={PresetId}, Name={PresetName}", sourceId, duplicated.Id, duplicated.Name);
    }

    [RelayCommand]
    public async Task DeleteSelectedPresetAsync()
    {
        if (SelectedPreset != null)
        {
            var presetToDelete = SelectedPreset;
            SelectedPreset = null; // Clear selection before deleting
            await _presetService.DeletePresetAsync(presetToDelete.Id);
            RefreshFilteredPresets();
            Status = $"Preset deleted: {presetToDelete.Name}";
            Log.Information("Preset deleted. Name={PresetName}, Id={PresetId}", presetToDelete.Name, presetToDelete.Id);
        }
        else
        {
            Status = "No preset selected to delete.";
        }
    }


    [RelayCommand]
    public async Task ExportSelectedPresetAsync(string filePath)
    {
        if (SelectedPreset == null)
        {
            Status = "No preset selected to export.";
            return;
        }

        await _presetService.ExportPresetAsync(filePath, SelectedPreset.Id);
        Status = $"Preset exported: {SelectedPreset.Name}";
    }

    [RelayCommand]
    public async Task ExportAllPresetsAsync(string filePath)
    {
        var ids = Presets.Select(p => p.Id).ToList();
        await _presetService.ExportPresetsAsync(filePath, ids);
        Status = $"Exported {ids.Count} presets.";
    }

    [RelayCommand]
    public async Task ImportPresetsAsync(string filePath)
    {
        var importedCount = await _presetService.ImportPresetsAsync(filePath);
        foreach (var preset in Presets)
        {
            EnsureTextStyleDefaults(preset);
        }

        RefreshFilteredPresets();
        SelectedPreset ??= FilteredPresets.FirstOrDefault();
        RefreshEditorCollections(SelectedPreset);
        Status = importedCount > 0
            ? $"Imported {importedCount} presets."
            : "No presets were imported.";
    }

    [RelayCommand]
    public async Task LoadAppSettingsAsync()
    {
        try
        {
            await _settingsService.LoadAsync();
            NdiConfig = CloneNdiConfig(_settingsService.Settings.Ndi);
            ConfigureAssetService(_settingsService.Settings.AssetPath);
            RefreshAssets();

            ShowDebugLogs = _settingsService.Settings.LogViewer.ShowDebug;
            ShowInformationLogs = _settingsService.Settings.LogViewer.ShowInformation;
            ShowWarningLogs = _settingsService.Settings.LogViewer.ShowWarning;
            ShowErrorLogs = _settingsService.Settings.LogViewer.ShowError;
            ShowFatalLogs = _settingsService.Settings.LogViewer.ShowFatal;
            LogKeyword = _settingsService.Settings.LogViewer.Keyword;
            AutoScrollLogs = _settingsService.Settings.LogViewer.AutoScroll;
            RefreshFilteredLogs();

            Status = "App settings loaded.";
            Log.Information("Application settings loaded.");
        }
        catch (Exception ex)
        {
            Status = $"Error loading app settings: {ex.Message}";
            Log.Error(ex, "Failed to load application settings.");
        }
    }

    [RelayCommand]
    public async Task SaveAppSettingsAsync()
    {
        try
        {
            _settingsService.Settings.Ndi = CloneNdiConfig(NdiConfig);
            _settingsService.Settings.LogViewer.ShowDebug = ShowDebugLogs;
            _settingsService.Settings.LogViewer.ShowInformation = ShowInformationLogs;
            _settingsService.Settings.LogViewer.ShowWarning = ShowWarningLogs;
            _settingsService.Settings.LogViewer.ShowError = ShowErrorLogs;
            _settingsService.Settings.LogViewer.ShowFatal = ShowFatalLogs;
            _settingsService.Settings.LogViewer.Keyword = LogKeyword;
            _settingsService.Settings.LogViewer.AutoScroll = AutoScrollLogs;
            await _settingsService.SaveAsync();
            Status = "App settings saved.";
            Log.Information("Application settings saved.");
        }
        catch (Exception ex)
        {
            Status = $"Error saving app settings: {ex.Message}";
            Log.Error(ex, "Failed to save application settings.");
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
            _hasNdiError = false;
            Status = "NDI Initialized.";
            Log.Information("NDI initialized from UI command.");
            RefreshNdiOutputStatus("初期化成功");
            _ndiSendTimer.Start();
        }
        catch (Exception ex)
        {
            _hasNdiError = true;
            Status = $"Error initializing NDI: {ex.Message}";
            Log.Error(ex, "Failed to initialize NDI from UI command.");
            RefreshNdiOutputStatus("初期化失敗");
        }
    }

    [RelayCommand]
    public async Task SetProgramActiveAsync(bool active)
    {
        try
        {
            await _ndiService.SetActiveAsync(NdiChannelType.Program, active);
            _hasNdiError = false;
            IsProgramActive = _ndiService.IsProgramActive;
            Status = $"NDI Program {(active ? "Active" : "Inactive")}.";
            RefreshNdiOutputStatus("Programチャンネル切替");
        }
        catch (Exception ex)
        {
            _hasNdiError = true;
            Status = $"Error updating NDI Program state: {ex.Message}";
            Log.Error(ex, "Failed to update NDI Program active state.");
            RefreshNdiOutputStatus("Programチャンネル切替失敗");
        }
    }

    [RelayCommand]
    public async Task SetPreviewActiveAsync(bool active)
    {
        try
        {
            await _ndiService.SetActiveAsync(NdiChannelType.Preview, active);
            _hasNdiError = false;
            IsPreviewActive = _ndiService.IsPreviewActive;
            Status = $"NDI Preview {(active ? "Active" : "Inactive")}.";
            RefreshNdiOutputStatus("Previewチャンネル切替");
        }
        catch (Exception ex)
        {
            _hasNdiError = true;
            Status = $"Error updating NDI Preview state: {ex.Message}";
            Log.Error(ex, "Failed to update NDI Preview active state.");
            RefreshNdiOutputStatus("Previewチャンネル切替失敗");
        }
    }

    private void EnsureTextStyleDefaults(Preset? preset)
    {
        if (preset == null)
        {
            return;
        }

        preset.EnsureTextBlocksInitialized();

        foreach (var block in preset.GetAllTextBlocks())
        {
            block.TextStyle ??= new TextStyleSettings();
            block.TextLayout ??= new TextLayoutSettings();

            if (string.IsNullOrWhiteSpace(block.TextStyle.FontFamily))
            {
                block.TextStyle.FontFamily = block.TextLines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line.FontFamily))?.FontFamily
                    ?? AvailableFontFamilies.FirstOrDefault()
                    ?? "Meiryo";
            }

            if (block.TextStyle.FontSize <= 0)
            {
                block.TextStyle.FontSize = block.TextLines.FirstOrDefault(line => line.FontSize > 0)?.FontSize ?? 48;
            }

            if (string.IsNullOrWhiteSpace(block.TextStyle.Color))
            {
                block.TextStyle.Color = block.TextLines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line.Color))?.Color ?? "#FFFFFF";
            }
        }
    }

    [RelayCommand]
    private void AddTextLine()
    {
        if (SelectedTextBlock == null)
        {
            return;
        }

        var defaultFontFamily = SelectedTextBlock.TextStyle.FontFamily;
        if (string.IsNullOrWhiteSpace(defaultFontFamily))
        {
            defaultFontFamily = AvailableFontFamilies.FirstOrDefault() ?? "Meiryo";
        }

        var defaultFontSize = SelectedTextBlock.TextStyle.FontSize > 0 ? SelectedTextBlock.TextStyle.FontSize : 48;
        var defaultColor = string.IsNullOrWhiteSpace(SelectedTextBlock.TextStyle.Color) ? "#FFFFFF" : SelectedTextBlock.TextStyle.Color;

        SelectedTextBlock.TextLines.Add(new TextLine
        {
            Text = "New line",
            FontFamily = defaultFontFamily,
            FontSize = defaultFontSize,
            Color = defaultColor
        });
    }

    [RelayCommand]
    private void RemoveTextLine(TextLine? line)
    {
        if (SelectedTextBlock == null || line == null)
        {
            return;
        }

        SelectedTextBlock.TextLines.Remove(line);
    }

    [RelayCommand]
    private void AddTextBlock()
    {
        if (SelectedPreset == null)
        {
            return;
        }

        SelectedPreset.EnsureTextBlocksInitialized();
        var blockIndex = EditableTextBlocks.Count + 1;
        var fontFamily = AvailableFontFamilies.FirstOrDefault() ?? "Meiryo";

        var block = new Models.TextBlock
        {
            Name = $"Text Block {blockIndex}",
            TextStyle = new TextStyleSettings { FontFamily = fontFamily, FontSize = 48, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings(),
            TextLines = [new TextLine { Text = $"Line {blockIndex}", FontFamily = fontFamily, FontSize = 48, Color = "#FFFFFF" }]
        };

        SelectedPreset.GetKeyer(KeyerDestination.Usk1).TextBlocks.Add(block);
        block.DestinationKeyer = KeyerDestination.Usk1;
        RefreshEditorCollections(SelectedPreset);
        SelectedTextBlock = block;
    }

    [RelayCommand]
    private void RemoveTextBlock(Models.TextBlock? block)
    {
        if (SelectedPreset == null || block == null)
        {
            return;
        }

        if (EditableTextBlocks.Count <= 1)
        {
            return;
        }

        var index = EditableTextBlocks.IndexOf(block);
        var keyer = SelectedPreset.GetKeyer(block.DestinationKeyer);
        keyer.TextBlocks.Remove(block);
        RefreshEditorCollections(SelectedPreset);
        SelectedTextBlock = EditableTextBlocks.ElementAtOrDefault(Math.Max(0, index - 1))
            ?? EditableTextBlocks.FirstOrDefault();
    }

    public Task ImportOverlayImageAsync(string sourcePath)
    {
        if (SelectedPreset == null)
        {
            Status = "No preset selected.";
            return Task.CompletedTask;
        }

        try
        {
            var relativePath = _assetService.ImportImage(sourcePath);
            var overlay = CreateOverlayItem(relativePath);
            SelectedPreset.GetKeyer(overlay.DestinationKeyer).Overlays.Add(overlay);
            RefreshEditorCollections(SelectedPreset);

            Status = $"Image imported: {relativePath}";
            Log.Information("Overlay image imported and attached: {RelativePath}", relativePath);
            OnPropertyChanged(nameof(SelectedPreset));
        }
        catch (Exception ex)
        {
            Status = $"Image import failed: {ex.Message}";
            Log.Error(ex, "Overlay image import failed.");
        }

        return Task.CompletedTask;
    }


    public void AddOverlayFromAsset(string relativePath, double dropX = 0, double dropY = 0, bool centerOnDrop = false)
    {
        if (SelectedPreset == null || string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var overlay = CreateOverlayItem(relativePath, dropX, dropY, centerOnDrop);
        SelectedPreset.GetKeyer(overlay.DestinationKeyer).Overlays.Add(overlay);
        RefreshEditorCollections(SelectedPreset);
        Status = $"Overlay set: {Path.GetFileName(relativePath)}";
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private OverlayItem CreateOverlayItem(string relativePath, double dropX = 0, double dropY = 0, bool centerOnDrop = false)
    {
        var width = 0;
        var height = 0;

        try
        {
            var resolvedPath = _assetService.ResolvePath(relativePath);
            if (File.Exists(resolvedPath))
            {
                using var bitmap = SKBitmap.Decode(resolvedPath);
                if (bitmap != null)
                {
                    width = bitmap.Width;
                    height = bitmap.Height;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not measure asset for overlay placement. Path={Path}", relativePath);
        }

        var x = centerOnDrop ? dropX - width / 2d : dropX;
        var y = centerOnDrop ? dropY - height / 2d : dropY;

        return new OverlayItem
        {
            DestinationKeyer = KeyerDestination.Usk1,
            Path = relativePath,
            X = (int)Math.Round(x),
            Y = (int)Math.Round(y),
            Width = width,
            Height = height,
            Opacity = 1.0,
            IsVisible = true
        };
    }


    [RelayCommand]
    public void RefreshAssets()
    {
        AssetItems.Clear();
        foreach (var asset in _assetService.GetAssets())
        {
            AssetItems.Add(asset);
        }

        Status = $"Assets refreshed: {AssetItems.Count}";
    }

    [RelayCommand]
    public void SetSelectedAssetAsOverlay()
    {
        if (SelectedPreset == null || SelectedAsset == null)
        {
            return;
        }

        AddOverlayFromAsset(SelectedAsset.RelativePath);
    }

    [RelayCommand]
    public void SetSelectedAssetAsBackground()
    {
        if (SelectedPreset == null || SelectedAsset == null)
        {
            return;
        }

        SelectedPreset.Background.Type = "image";
        SelectedPreset.Background.AssetPath = SelectedAsset.RelativePath;
        SelectedPreset.Background.Alpha = 1.0;
        Status = $"Background set: {SelectedAsset.FileName}";
        OnPropertyChanged(nameof(SelectedPreset));
    }

    [RelayCommand]
    private void ToggleKeyer(KeyerSlot? keyer)
    {
        if (keyer == null)
        {
            return;
        }

        keyer.KeyOn = !keyer.KeyOn;
        RefreshEditorCollections(SelectedPreset);
        OnPropertyChanged(nameof(SelectedPreset));
    }

    public void ReportSelectionAlignment(string description)
    {
        Status = description;
        OnPropertyChanged(nameof(SelectedPreset));
    }

    private void ConfigureAssetService(string assetPath)
    {
        if (string.IsNullOrWhiteSpace(assetPath))
        {
            return;
        }

        _assetService.AssetsChanged -= AssetService_AssetsChanged;
        _assetService.Dispose();
        _assetService = new AssetService(assetPath);
        _assetService.AssetsChanged += AssetService_AssetsChanged;
    }

    private void AssetService_AssetsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RefreshAssets();
        });
    }

    public IAsyncRelayCommand<Preset> ShowPresetCommand { get; }

    [RelayCommand]
    private Task SelectPreviewPresetAsync(Preset? preset)
    {
        if (preset == null)
        {
            Status = "No preset selected for preview.";
            return Task.CompletedTask;
        }

        if (CurrentPreviewPreset == preset)
        {
            Status = $"Preview preset already selected: {preset.Name}";
            return Task.CompletedTask;
        }

        CurrentPreviewPreset = preset;
        Status = $"Preview preset selected: {preset.Name}";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task TakeAsync()
    {
        if (CurrentPreviewPreset == null)
        {
            Status = "TAKE ignored: preview preset is not set.";
            return Task.CompletedTask;
        }

        if (!IsProgramActive)
        {
            Status = "TAKE ignored: Program channel is inactive.";
            return Task.CompletedTask;
        }

        return ApplyProgramPresetAsync(CurrentPreviewPreset, immediate: false, actionName: "TAKE");
    }

    [RelayCommand]
    private Task CutAsync()
    {
        if (CurrentPreviewPreset == null)
        {
            Status = "CUT ignored: preview preset is not set.";
            return Task.CompletedTask;
        }

        if (!IsProgramActive)
        {
            Status = "CUT ignored: Program channel is inactive.";
            return Task.CompletedTask;
        }

        return ApplyProgramPresetAsync(CurrentPreviewPreset, immediate: true, actionName: "CUT");
    }

    private Task ShowPresetAsync(Preset? preset)
    {
        if (preset == null)
        {
            Status = "No preset selected to show.";
            return Task.CompletedTask;
        }

        CancelAutoClear("manual operation");
        CurrentPreviewPreset = preset;
        return ApplyProgramPresetAsync(preset, immediate: true, actionName: "Show");
    }

    private Task ApplyProgramPresetAsync(Preset preset, bool immediate, string actionName)
    {
        if (CurrentProgramPreset == preset)
        {
            CancelAutoClear("preset redisplay");
            Status = $"{actionName} ignored: preset already on Program ({preset.Name}).";
            return Task.CompletedTask;
        }

        if (immediate)
        {
            _transitionTimer?.Stop();
            _transitionFromPreset = null;
            _transitionToPreset = null;
            _transitionProgress = 1f;
        }
        else
        {
            _transitionFromPreset = CurrentProgramPreset ?? new Preset(); // If null, transition from empty
            _transitionToPreset = preset;
            _transitionProgress = 0f;

            _transitionTimer?.Stop();
            _transitionTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Normal, TransitionTimer_Tick);
            _transitionTimer.Start();
        }

        CurrentProgramPreset = preset;
        Status = $"{actionName}: {preset.Name}";

        StartAutoClear(preset);

        return Task.CompletedTask;
    }

    private void StartAutoClear(Preset preset)
    {
        if (_autoClearTimer == null)
        {
            return;
        }

        if (preset.AutoClearSeconds <= 0)
        {
            CancelAutoClear("preset AutoClear disabled");
            return;
        }

        _autoClearEnabled = true;
        AutoClearRemainingSeconds = preset.AutoClearSeconds;
        OnPropertyChanged(nameof(AutoClearStatusText));
        _autoClearTimer.Stop();
        _autoClearTimer.Start();
    }

    private void CancelAutoClear(string reason)
    {
        _autoClearEnabled = false;
        _autoClearTimer?.Stop();
        if (AutoClearRemainingSeconds == 0)
        {
            return;
        }

        AutoClearRemainingSeconds = 0;
        OnPropertyChanged(nameof(AutoClearStatusText));
        Log.Information("AutoClear cancelled: {Reason}", reason);
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
        await HandleAutoClearTickAsync();
    }

    public async Task HandleAutoClearTickAsync()
    {
        if (!_autoClearEnabled || CurrentProgramPreset == null || CurrentProgramPreset.AutoClearSeconds <= 0)
        {
            return;
        }

        if (!_ndiService.IsProgramActive)
        {
            CancelAutoClear("program channel inactive");
            return;
        }

        if (AutoClearRemainingSeconds > 0)
        {
            AutoClearRemainingSeconds--;
            OnPropertyChanged(nameof(AutoClearStatusText));
        }

        if (AutoClearRemainingSeconds <= 0)
        {
            await ClearProgram();
            CancelAutoClear("timer elapsed");
        }
    }

    [RelayCommand]
    public async Task ClearProgram()
    {
        CancelAutoClear("manual clear");
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
        CurrentPreviewPreset = null;
        Status = "Program cleared.";
        Log.Information("Program output cleared.");
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

    private ExternalBasicSettings CreateExternalBasicSettings()
    {
        return new ExternalBasicSettings
        {
            NdiSourceName = NdiConfig.SourceName,
            ResolutionWidth = NdiConfig.ResolutionWidth,
            ResolutionHeight = NdiConfig.ResolutionHeight,
            FrameRateN = NdiConfig.FrameRateN,
            FrameRateD = NdiConfig.FrameRateD,
            WebApiPort = _settingsService.Settings.WebApiPort,
            OscPort = _settingsService.Settings.OscPort
        };
    }

    private async void NdiSendTimer_Tick(object? sender, EventArgs e)
    {
        if (!_ndiService.IsInitialized) return;

        try
        {
            if (CurrentProgramPreset != null)
            {
                SKBitmap programBitmap;
                if (_transitionFromPreset != null && _transitionToPreset != null)
                {
                    programBitmap = _renderService.RenderTransition(_transitionFromPreset, _transitionToPreset, _transitionProgress, _transitionToPreset.Animation, NdiConfig);
                }
                else
                {
                    programBitmap = _renderService.Render(CurrentProgramPreset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);
                }

                using (programBitmap)
                {
                    await _ndiService.SendFrameAsync(NdiChannelType.Program, programBitmap);
                }
            }

            if (CurrentPreviewPreset != null)
            {
                using var previewBitmap = _renderService.Render(CurrentPreviewPreset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);
                await _ndiService.SendFrameAsync(NdiChannelType.Preview, previewBitmap);
            }
        }
        catch (Exception ex)
        {
            _hasNdiError = true;
            Status = $"Error sending NDI frame: {ex.Message}";
            Log.Error(ex, "Failed sending NDI frame.");
            RefreshNdiOutputStatus("送信エラー");
            _ndiSendTimer.Stop();
        }
    }

    private void RefreshNdiOutputStatus(string reason)
    {
        var nextStatus = "Inactive";
        var nextColor = "#888888";

        if (_hasNdiError)
        {
            nextStatus = "Error";
            nextColor = "#E74C3C";
        }
        else if (_ndiService.IsInitialized && (_ndiService.IsProgramActive || _ndiService.IsPreviewActive))
        {
            nextStatus = "Active";
            nextColor = "#3CB371";
        }

        if (NdiOutputStatus == nextStatus && NdiOutputStatusColor == nextColor)
        {
            return;
        }

        NdiOutputStatus = nextStatus;
        NdiOutputStatusColor = nextColor;
        Log.Information("NDI出力ステータス更新: {Status} ({Reason})", nextStatus, reason);
    }
}
