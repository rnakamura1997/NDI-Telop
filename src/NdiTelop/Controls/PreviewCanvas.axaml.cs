using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NdiTelop.Models;
using ModelTextBlock = NdiTelop.Models.TextBlock;
using NdiTelop.Services;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace NdiTelop.Controls;

public partial class PreviewCanvas : UserControl
{
    private const double HandleRadius = 5d;
    private static readonly Pen SelectionPen = new(new SolidColorBrush(Color.Parse("#00B7FF")), 2);
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#00B7FF"));

    private readonly RenderService _renderService;
    private readonly Dictionary<ModelTextBlock, Rect> _textBlockBounds = [];
    private readonly List<ModelTextBlock> _subscribedBlocks = [];
    private SKBitmap? _renderedBitmap;
    private Preset? _subscribedPreset;
    private bool _isDragging;
    private Point _dragStartPoint;
    private float _dragStartOffsetX;
    private float _dragStartOffsetY;

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
            EnsureSelectedTextBlockBelongsToPreset();
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
        UpdateTextBlockBounds();

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

            if (SelectedTextBlock != null && _textBlockBounds.TryGetValue(SelectedTextBlock, out var selectedBounds))
            {
                context.DrawRectangle(null, SelectionPen, selectedBounds);
                foreach (var handleCenter in GetHandleCenters(selectedBounds))
                {
                    context.DrawEllipse(SelectionFill, SelectionPen, handleCenter, HandleRadius, HandleRadius);
                }
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
        var block = HitTestBlock(point);
        SelectedTextBlock = block;

        if (block != null)
        {
            _isDragging = true;
            _dragStartPoint = point;
            _dragStartOffsetX = block.TextLayout.OffsetX;
            _dragStartOffsetY = block.TextLayout.OffsetY;
            e.Pointer.Capture(this);
        }
        else
        {
            _isDragging = false;
            e.Pointer.Capture(null);
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging || SelectedTextBlock == null || NdiConfig == null)
        {
            return;
        }

        var point = ToRenderPoint(e.GetPosition(this));
        SelectedTextBlock.TextLayout.OffsetX = _dragStartOffsetX + (float)(point.X - _dragStartPoint.X);
        SelectedTextBlock.TextLayout.OffsetY = _dragStartOffsetY + (float)(point.Y - _dragStartPoint.Y);
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
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual();
    }

    private void EndDrag(PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    private void EnsureSelectedTextBlockBelongsToPreset()
    {
        if (SelectedTextBlock != null && Preset?.TextBlocks.Contains(SelectedTextBlock) == true)
        {
            return;
        }

        SelectedTextBlock = Preset?.TextBlocks.FirstOrDefault();
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

    private ModelTextBlock? HitTestBlock(Point point)
    {
        foreach (var block in Preset?.TextBlocks.Reverse() ?? Enumerable.Empty<ModelTextBlock>())
        {
            if (_textBlockBounds.TryGetValue(block, out var bounds) && bounds.Contains(point))
            {
                return block;
            }
        }

        return null;
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

        _subscribedPreset = preset;
        preset.TextBlocks.CollectionChanged += TextBlocks_CollectionChanged;
        SubscribeToBlocks(preset.TextBlocks);
    }

    private void UnsubscribeFromPreset(Preset? preset)
    {
        if (preset == null)
        {
            return;
        }

        preset.TextBlocks.CollectionChanged -= TextBlocks_CollectionChanged;
        UnsubscribeFromBlocks(_subscribedBlocks.ToArray());
        _subscribedPreset = null;
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

        EnsureSelectedTextBlockBelongsToPreset();
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
}
