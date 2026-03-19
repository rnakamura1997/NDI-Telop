using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NdiTelop.Models;
using ModelTextBlock = NdiTelop.Models.TextBlock;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace NdiTelop.Controls;

public partial class PreviewCanvas : UserControl
{
    private const string AssetDragDataFormat = "application/x-nditelop-asset-path";
    private const double HandleRadius = 5d;
    private static readonly Pen SelectionPen = new(new SolidColorBrush(Color.Parse("#00B7FF")), 2);
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#00B7FF"));

    private readonly RenderService _renderService;
    private readonly Dictionary<ModelTextBlock, Rect> _textBlockBounds = [];
    private readonly Dictionary<OverlayItem, Rect> _overlayBounds = [];
    private readonly List<ModelTextBlock> _subscribedBlocks = [];
    private readonly List<OverlayItem> _subscribedOverlays = [];
    private SKBitmap? _renderedBitmap;
    private bool _isDragging;
    private Point _dragStartPoint;
    private float _dragStartOffsetX;
    private float _dragStartOffsetY;
    private DragTargetType _dragTargetType;
    private OverlayItem? _selectedOverlay;

    public static readonly DirectProperty<PreviewCanvas, Preset?> PresetProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, Preset?>(
            nameof(Preset), o => o.Preset, (o, v) => o.Preset = v);

    public static readonly DirectProperty<PreviewCanvas, ModelTextBlock?> SelectedTextBlockProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, ModelTextBlock?>(
            nameof(SelectedTextBlock), o => o.SelectedTextBlock, (o, v) => o.SelectedTextBlock = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly DirectProperty<PreviewCanvas, NdiConfig?> NdiConfigProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, NdiConfig?>(
            nameof(NdiConfig), o => o.NdiConfig, (o, v) => o.NdiConfig = v);

    private Preset? _preset;
    public Preset? Preset
    {
        get => _preset;
        set
        {
            if (_preset == value)
            {
                return;
            }

            UnsubscribeFromPreset(_preset);
            SetAndRaise(PresetProperty, ref _preset, value);
            SubscribeToPreset(_preset);
            EnsureSelectionBelongsToPreset();
            InvalidateVisual();
        }
    }

    private ModelTextBlock? _selectedTextBlock;
    public ModelTextBlock? SelectedTextBlock
    {
        get => _selectedTextBlock;
        set
        {
            if (_selectedTextBlock == value)
            {
                return;
            }

            SetAndRaise(SelectedTextBlockProperty, ref _selectedTextBlock, value);
            if (value != null)
            {
                _selectedOverlay = null;
            }

            InvalidateVisual();
        }
    }

    private NdiConfig? _ndiConfig;
    public NdiConfig? NdiConfig
    {
        get => _ndiConfig;
        set
        {
            SetAndRaise(NdiConfigProperty, ref _ndiConfig, value);
            InvalidateVisual();
        }
    }

    public PreviewCanvas()
    {
        InitializeComponent();
        _renderService = new RenderService();
        ClipToBounds = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, PreviewCanvas_OnDragOver);
        AddHandler(DragDrop.DropEvent, PreviewCanvas_OnDrop);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Preset == null || NdiConfig == null)
        {
            return;
        }

        _renderedBitmap?.Dispose();
        _renderedBitmap = _renderService.Render(Preset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);
        UpdateBounds();

        if (_renderedBitmap == null)
        {
            return;
        }

        var scaleX = Bounds.Width / NdiConfig.ResolutionWidth;
        var scaleY = Bounds.Height / NdiConfig.ResolutionHeight;
        var scaleMatrix = Matrix.CreateScale(scaleX, scaleY);

        using (context.PushTransform(scaleMatrix))
        {
            using var encoded = _renderedBitmap.Encode(SKEncodedImageFormat.Png, 100);
            using var image = new Avalonia.Media.Imaging.Bitmap(encoded.AsStream());
            context.DrawImage(image, new Rect(0, 0, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight));

            if (_selectedOverlay != null && _overlayBounds.TryGetValue(_selectedOverlay, out var selectedOverlayBounds))
            {
                DrawSelection(context, selectedOverlayBounds);
            }
            else if (SelectedTextBlock != null && _textBlockBounds.TryGetValue(SelectedTextBlock, out var selectedTextBounds))
            {
                DrawSelection(context, selectedTextBounds);
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (Preset == null || NdiConfig == null)
        {
            return;
        }

        var point = ToRenderPoint(e.GetPosition(this));
        var hit = HitTestItem(point);

        switch (hit.TargetType)
        {
            case DragTargetType.Overlay when hit.Overlay != null:
                _selectedOverlay = hit.Overlay;
                SelectedTextBlock = null;
                _dragTargetType = DragTargetType.Overlay;
                _dragStartOffsetX = hit.Overlay.X;
                _dragStartOffsetY = hit.Overlay.Y;
                BeginDrag(e, point);
                break;

            case DragTargetType.TextBlock when hit.TextBlock != null:
                _selectedOverlay = null;
                SelectedTextBlock = hit.TextBlock;
                _dragTargetType = DragTargetType.TextBlock;
                _dragStartOffsetX = hit.TextBlock.TextLayout.OffsetX;
                _dragStartOffsetY = hit.TextBlock.TextLayout.OffsetY;
                BeginDrag(e, point);
                break;

            default:
                _selectedOverlay = null;
                SelectedTextBlock = null;
                _dragTargetType = DragTargetType.None;
                _isDragging = false;
                e.Pointer.Capture(null);
                break;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging || NdiConfig == null)
        {
            return;
        }

        var point = ToRenderPoint(e.GetPosition(this));
        switch (_dragTargetType)
        {
            case DragTargetType.TextBlock when SelectedTextBlock != null:
                SelectedTextBlock.TextLayout.OffsetX = _dragStartOffsetX + (float)(point.X - _dragStartPoint.X);
                SelectedTextBlock.TextLayout.OffsetY = _dragStartOffsetY + (float)(point.Y - _dragStartPoint.Y);
                break;

            case DragTargetType.Overlay when _selectedOverlay != null:
                _selectedOverlay.X = (int)Math.Round(_dragStartOffsetX + (point.X - _dragStartPoint.X));
                _selectedOverlay.Y = (int)Math.Round(_dragStartOffsetY + (point.Y - _dragStartPoint.Y));
                break;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        EndDrag(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _dragTargetType = DragTargetType.None;
    }

    private void PreviewCanvas_OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasAssetData(e) && Preset != null && NdiConfig != null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void PreviewCanvas_OnDrop(object? sender, DragEventArgs e)
    {

        if (!HasAssetData(e) || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var relativePath = e.Data.Get(AssetDragDataFormat) as string ?? e.Data.GetText();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        var point = ToRenderPoint(e.GetPosition(this));
        viewModel.AddOverlayFromAsset(relativePath, point.X, point.Y, centerOnDrop: true);
        _selectedOverlay = Preset?.Overlays.LastOrDefault();
        SelectedTextBlock = null;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    private static bool HasAssetData(DragEventArgs e) => e.Data.Contains(AssetDragDataFormat);

    private void BeginDrag(PointerPressedEventArgs e, Point point)
    {
        _isDragging = true;
        _dragStartPoint = point;
        e.Pointer.Capture(this);
    }

    private void EndDrag(PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _dragTargetType = DragTargetType.None;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    private void DrawSelection(DrawingContext context, Rect bounds)
    {
        context.DrawRectangle(null, SelectionPen, bounds);
        foreach (var handleCenter in GetHandleCenters(bounds))
        {
            context.DrawEllipse(SelectionFill, SelectionPen, handleCenter, HandleRadius, HandleRadius);
        }
    }

    private void EnsureSelectionBelongsToPreset()
    {
        if (SelectedTextBlock != null && Preset?.TextBlocks.Contains(SelectedTextBlock) != true)
        {
            SelectedTextBlock = null;
        }

        if (_selectedOverlay != null && Preset?.Overlays.Contains(_selectedOverlay) != true)
        {
            _selectedOverlay = null;
        }

        if (_selectedOverlay == null && SelectedTextBlock == null)
        {
            SelectedTextBlock = Preset?.TextBlocks.FirstOrDefault();
        }
    }

    private Point ToRenderPoint(Point point)
    {
        if (NdiConfig == null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return point;
        }

        return new Point(
            point.X * NdiConfig.ResolutionWidth / Bounds.Width,
            point.Y * NdiConfig.ResolutionHeight / Bounds.Height);
    }

    private HitTestResult HitTestItem(Point point)
    {
        foreach (var overlay in Preset?.Overlays.Reverse() ?? Enumerable.Empty<OverlayItem>())
        {
            if (_overlayBounds.TryGetValue(overlay, out var bounds) && bounds.Contains(point))
            {
                return new HitTestResult(DragTargetType.Overlay, null, overlay);
            }
        }

        foreach (var block in Preset?.TextBlocks.Reverse() ?? Enumerable.Empty<ModelTextBlock>())
        {
            if (_textBlockBounds.TryGetValue(block, out var bounds) && bounds.Contains(point))
            {
                return new HitTestResult(DragTargetType.TextBlock, block, null);
            }
        }

        return new HitTestResult(DragTargetType.None, null, null);
    }

    private void UpdateBounds()
    {
        UpdateTextBlockBounds();
        UpdateOverlayBounds();
    }

    private void UpdateTextBlockBounds()
    {
        _textBlockBounds.Clear();

        if (Preset == null || NdiConfig == null)
        {
            return;
        }

        foreach (var block in Preset.TextBlocks)
        {
            if (TryMeasureBlock(block, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight, out var bounds))
            {
                _textBlockBounds[block] = bounds.Inflate(8);
            }
        }
    }

    private void UpdateOverlayBounds()
    {
        _overlayBounds.Clear();

        foreach (var overlay in Preset?.Overlays ?? [])
        {
            if (!overlay.IsVisible)
            {
                continue;
            }

            var width = Math.Max(1, overlay.Width);
            var height = Math.Max(1, overlay.Height);
            _overlayBounds[overlay] = new Rect(overlay.X, overlay.Y, width, height).Inflate(6);
        }
    }

    private static bool TryMeasureBlock(ModelTextBlock block, int width, int height, out Rect bounds)
    {
        bounds = default;
        if (block.TextLines.Count == 0)
        {
            return false;
        }

        const float lineSpacing = 10f;
        var lineBounds = new List<SKRect>(block.TextLines.Count);

        foreach (var line in block.TextLines)
        {
            var fontFamily = !string.IsNullOrWhiteSpace(block.TextStyle.FontFamily) ? block.TextStyle.FontFamily : line.FontFamily;
            var fontSize = Math.Clamp(block.TextStyle.FontSize > 0 ? block.TextStyle.FontSize : line.FontSize, 8, 300);
            using var typeface = SKTypeface.FromFamilyName(fontFamily) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, fontSize);
            font.MeasureText(line.Text, out var measured);
            lineBounds.Add(measured);
        }

        var totalHeight = lineBounds.Sum(x => x.Height) + lineSpacing * Math.Max(0, lineBounds.Count - 1);
        var layout = block.TextLayout;
        var top = (layout.VerticalAlignment switch
        {
            VerticalTextAlignment.Top => 0f,
            VerticalTextAlignment.Bottom => height - totalHeight,
            _ => (height - totalHeight) / 2f
        }) + layout.OffsetY;

        Rect union = default;
        var hasBounds = false;
        var currentTop = top;
        for (var i = 0; i < lineBounds.Count; i++)
        {
            var measured = lineBounds[i];
            var left = (layout.HorizontalAlignment switch
            {
                HorizontalTextAlignment.Left => -measured.Left,
                HorizontalTextAlignment.Right => width - measured.Right,
                _ => (width - measured.Width) / 2f - measured.Left
            }) + layout.OffsetX;

            var rect = new Rect(left + measured.Left, currentTop, measured.Width, measured.Height);
            union = hasBounds ? union.Union(rect) : rect;
            hasBounds = true;
            currentTop += measured.Height + lineSpacing;
        }

        bounds = union;
        return hasBounds;
    }

    private IEnumerable<Point> GetHandleCenters(Rect rect)
    {
        yield return rect.TopLeft;
        yield return rect.TopRight;
        yield return rect.BottomLeft;
        yield return rect.BottomRight;
    }

    private void SubscribeToPreset(Preset? preset)
    {
        if (preset == null)
        {
            return;
        }

        preset.TextBlocks.CollectionChanged += TextBlocks_CollectionChanged;
        preset.Overlays.CollectionChanged += Overlays_CollectionChanged;
        SubscribeToBlocks(preset.TextBlocks);
        SubscribeToOverlays(preset.Overlays);
    }

    private void UnsubscribeFromPreset(Preset? preset)
    {
        if (preset == null)
        {
            return;
        }

        preset.TextBlocks.CollectionChanged -= TextBlocks_CollectionChanged;
        preset.Overlays.CollectionChanged -= Overlays_CollectionChanged;
        UnsubscribeFromBlocks(_subscribedBlocks.ToArray());
        UnsubscribeFromOverlays(_subscribedOverlays.ToArray());
    }

    private void TextBlocks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            UnsubscribeFromBlocks(e.OldItems.OfType<ModelTextBlock>());
        }

        if (e.NewItems != null)
        {
            SubscribeToBlocks(e.NewItems.OfType<ModelTextBlock>());
        }

        EnsureSelectionBelongsToPreset();
        InvalidateVisual();
    }

    private void Overlays_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            UnsubscribeFromOverlays(e.OldItems.OfType<OverlayItem>());
        }

        if (e.NewItems != null)
        {
            SubscribeToOverlays(e.NewItems.OfType<OverlayItem>());
        }

        EnsureSelectionBelongsToPreset();
        InvalidateVisual();
    }

    private void SubscribeToBlocks(IEnumerable<ModelTextBlock> blocks)
    {
        foreach (var block in blocks)
        {
            if (_subscribedBlocks.Contains(block))
            {
                continue;
            }

            _subscribedBlocks.Add(block);
            block.TextLines.CollectionChanged += TextLines_CollectionChanged;
            block.TextLayout.PropertyChanged += NestedObject_PropertyChanged;
            block.TextStyle.PropertyChanged += NestedObject_PropertyChanged;
            foreach (var line in block.TextLines)
            {
                line.PropertyChanged += NestedObject_PropertyChanged;
            }
        }
    }

    private void UnsubscribeFromBlocks(IEnumerable<ModelTextBlock> blocks)
    {
        foreach (var block in blocks.ToArray())
        {
            block.TextLines.CollectionChanged -= TextLines_CollectionChanged;
            block.TextLayout.PropertyChanged -= NestedObject_PropertyChanged;
            block.TextStyle.PropertyChanged -= NestedObject_PropertyChanged;
            foreach (var line in block.TextLines)
            {
                line.PropertyChanged -= NestedObject_PropertyChanged;
            }

            _subscribedBlocks.Remove(block);
        }
    }

    private void SubscribeToOverlays(IEnumerable<OverlayItem> overlays)
    {
        foreach (var overlay in overlays)
        {
            if (_subscribedOverlays.Contains(overlay))
            {
                continue;
            }

            _subscribedOverlays.Add(overlay);
            overlay.PropertyChanged += NestedObject_PropertyChanged;
        }
    }

    private void UnsubscribeFromOverlays(IEnumerable<OverlayItem> overlays)
    {
        foreach (var overlay in overlays.ToArray())
        {
            overlay.PropertyChanged -= NestedObject_PropertyChanged;
            _subscribedOverlays.Remove(overlay);
        }
    }

    private void TextLines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var line in e.OldItems.OfType<TextLine>())
            {
                line.PropertyChanged -= NestedObject_PropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var line in e.NewItems.OfType<TextLine>())
            {
                line.PropertyChanged += NestedObject_PropertyChanged;
            }
        }

        InvalidateVisual();
    }

    private void NestedObject_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private enum DragTargetType
    {
        None,
        TextBlock,
        Overlay
    }

    private readonly record struct HitTestResult(DragTargetType TargetType, ModelTextBlock? TextBlock, OverlayItem? Overlay);
}
