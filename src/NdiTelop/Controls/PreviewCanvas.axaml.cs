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
    private const double HandleHitRadius = 10d;
    private static readonly Pen SelectionPen = new(new SolidColorBrush(Color.Parse("#00B7FF")), 2);
    private static readonly Pen GroupSelectionPen = new(new SolidColorBrush(Color.Parse("#7C4DFF")), 2, dashStyle: new DashStyle([6, 4], 0));
    private static readonly Pen RubberBandPen = new(new SolidColorBrush(Color.Parse("#FFFFFF")), 1, dashStyle: new DashStyle([4, 4], 0));
    private static readonly IBrush SelectionFill = new SolidColorBrush(Color.Parse("#00B7FF"));
    private static readonly IBrush GroupSelectionFill = new SolidColorBrush(Color.FromArgb(32, 124, 77, 255));
    private static readonly IBrush RubberBandFill = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));

    private readonly RenderService _renderService;
    private readonly Dictionary<ModelTextBlock, Rect> _textBlockBounds = [];
    private readonly Dictionary<OverlayItem, Rect> _overlayBounds = [];
    private readonly List<ModelTextBlock> _subscribedBlocks = [];
    private readonly List<OverlayItem> _subscribedOverlays = [];
    private readonly HashSet<ModelTextBlock> _selectedTextBlocks = [];
    private readonly HashSet<OverlayItem> _selectedOverlays = [];
    private readonly Dictionary<ModelTextBlock, Point> _dragStartTextPositions = [];
    private readonly Dictionary<OverlayItem, Point> _dragStartOverlayPositions = [];
    private readonly List<SelectionEntry> _selectionOrder = [];
    private SKBitmap? _renderedBitmap;
    private bool _isDragging;
    private bool _isRubberBandSelecting;
    private Point _dragStartPoint;
    private float _dragStartOffsetX;
    private float _dragStartOffsetY;
    private Rect _dragStartOverlayBounds;
    private double _dragAspectRatio = 1d;
    private DragTargetType _dragTargetType;
    private OverlayItem? _selectedOverlay;
    private Rect? _rubberBandRect;

    public static readonly DirectProperty<PreviewCanvas, Preset?> PresetProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, Preset?>(
            nameof(Preset), o => o.Preset, (o, v) => o.Preset = v);

    public static readonly DirectProperty<PreviewCanvas, ModelTextBlock?> SelectedTextBlockProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, ModelTextBlock?>(
            nameof(SelectedTextBlock), o => o.SelectedTextBlock, (o, v) => o.SelectedTextBlock = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly DirectProperty<PreviewCanvas, NdiConfig?> NdiConfigProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, NdiConfig?>(
            nameof(NdiConfig), o => o.NdiConfig, (o, v) => o.NdiConfig = v);

    public static readonly DirectProperty<PreviewCanvas, SelectionAlignmentReferenceMode> AlignmentReferenceModeProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, SelectionAlignmentReferenceMode>(
            nameof(AlignmentReferenceMode), o => o.AlignmentReferenceMode, (o, v) => o.AlignmentReferenceMode = v);

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

    private SelectionAlignmentReferenceMode _alignmentReferenceMode = SelectionAlignmentReferenceMode.SelectionBounds;
    public SelectionAlignmentReferenceMode AlignmentReferenceMode
    {
        get => _alignmentReferenceMode;
        set => SetAndRaise(AlignmentReferenceModeProperty, ref _alignmentReferenceMode, value);
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
            DrawSelections(context);

            if (_rubberBandRect is { } rubberBandRect)
            {
                context.DrawRectangle(RubberBandFill, RubberBandPen, rubberBandRect);
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

        var modifiers = e.KeyModifiers;
        var allowMultiSelect = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift);
        var point = ToRenderPoint(e.GetPosition(this));
        var hit = HitTestItem(point);

        switch (hit.TargetType)
        {
            case DragTargetType.OverlayResize when hit.Overlay != null && hit.ResizeHandle != ResizeHandle.None && _selectedOverlays.Count == 1 && _selectedTextBlocks.Count == 0:
                SelectOverlay(hit.Overlay, append: false);
                _dragTargetType = DragTargetType.OverlayResize;
                _dragStartOverlayBounds = GetOverlayRect(hit.Overlay);
                _dragAspectRatio = GetOverlayAspectRatio(hit.Overlay, _dragStartOverlayBounds);
                _activeResizeHandle = hit.ResizeHandle;
                BeginDrag(e, point);
                break;

            case DragTargetType.Overlay when hit.Overlay != null:
                UpdateSelectionForOverlay(hit.Overlay, allowMultiSelect);
                _dragTargetType = _selectedOverlays.Count + _selectedTextBlocks.Count > 1 ? DragTargetType.Group : DragTargetType.Overlay;
                _dragStartOffsetX = hit.Overlay.X;
                _dragStartOffsetY = hit.Overlay.Y;
                _activeResizeHandle = ResizeHandle.None;
                CaptureDragSelectionSnapshot();
                BeginDrag(e, point);
                break;

            case DragTargetType.TextBlock when hit.TextBlock != null:
                UpdateSelectionForTextBlock(hit.TextBlock, allowMultiSelect);
                _dragTargetType = _selectedOverlays.Count + _selectedTextBlocks.Count > 1 ? DragTargetType.Group : DragTargetType.TextBlock;
                _dragStartOffsetX = hit.TextBlock.TextLayout.OffsetX;
                _dragStartOffsetY = hit.TextBlock.TextLayout.OffsetY;
                _activeResizeHandle = ResizeHandle.None;
                CaptureDragSelectionSnapshot();
                BeginDrag(e, point);
                break;

            default:
                _dragTargetType = DragTargetType.RubberBand;
                _activeResizeHandle = ResizeHandle.None;
                _isRubberBandSelecting = true;
                _rubberBandRect = new Rect(point, point);
                if (!allowMultiSelect)
                {
                    ClearSelection();
                }

                BeginDrag(e, point);
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

            case DragTargetType.Group:
                MoveSelectedItems(point.X - _dragStartPoint.X, point.Y - _dragStartPoint.Y);
                break;

            case DragTargetType.OverlayResize when _selectedOverlay != null && _activeResizeHandle != ResizeHandle.None:
                ResizeOverlay(_selectedOverlay, point, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
                break;

            case DragTargetType.RubberBand:
                _rubberBandRect = CreateNormalizedRect(_dragStartPoint, point);
                break;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isRubberBandSelecting && _rubberBandRect is { } rubberBandRect)
        {
            ApplyRubberBandSelection(rubberBandRect, e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        }

        EndDrag(e);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _isDragging = false;
        _isRubberBandSelecting = false;
        _dragTargetType = DragTargetType.None;
        _rubberBandRect = null;
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
        if (Preset?.Overlays.LastOrDefault() is { } overlay)
        {
            SelectOverlay(overlay, append: false);
        }

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
            _isRubberBandSelecting = false;
            _dragTargetType = DragTargetType.None;
            _activeResizeHandle = ResizeHandle.None;
            _rubberBandRect = null;
            _dragStartTextPositions.Clear();
            _dragStartOverlayPositions.Clear();
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    private void DrawSelections(DrawingContext context)
    {
        foreach (var overlay in _selectedOverlays)
        {
            if (_overlayBounds.TryGetValue(overlay, out var bounds))
            {
                DrawSelection(context, bounds, showHandles: _selectedOverlays.Count == 1 && _selectedTextBlocks.Count == 0 && overlay == _selectedOverlay, isGroupMember: SelectionCount > 1);
            }
        }

        foreach (var block in _selectedTextBlocks)
        {
            if (_textBlockBounds.TryGetValue(block, out var bounds))
            {
                DrawSelection(context, bounds, showHandles: false, isGroupMember: SelectionCount > 1);
            }
        }

        if (SelectionCount > 1 && TryGetSelectionBounds(out var groupBounds))
        {
            context.DrawRectangle(GroupSelectionFill, GroupSelectionPen, groupBounds.Inflate(8));
        }
    }

    private void DrawSelection(DrawingContext context, Rect bounds, bool showHandles, bool isGroupMember)
    {
        var pen = isGroupMember ? GroupSelectionPen : SelectionPen;
        context.DrawRectangle(null, pen, bounds);
        if (!showHandles)
        {
            return;
        }

        foreach (var handleCenter in GetHandleCenters(bounds))
        {
            context.DrawEllipse(SelectionFill, SelectionPen, handleCenter, HandleRadius, HandleRadius);
        }
    }

    private void EnsureSelectionBelongsToPreset()
    {
        _selectedTextBlocks.RemoveWhere(block => Preset?.TextBlocks.Contains(block) != true);
        _selectedOverlays.RemoveWhere(overlay => Preset?.Overlays.Contains(overlay) != true);
        _selectionOrder.RemoveAll(entry => entry.Type switch
        {
            SelectionItemType.TextBlock => entry.Item is not ModelTextBlock block || Preset?.TextBlocks.Contains(block) != true,
            SelectionItemType.Overlay => entry.Item is not OverlayItem overlay || Preset?.Overlays.Contains(overlay) != true,
            _ => true
        });

        if (SelectedTextBlock != null && !_selectedTextBlocks.Contains(SelectedTextBlock))
        {
            SelectedTextBlock = _selectedTextBlocks.FirstOrDefault();
        }

        if (_selectedOverlay != null && !_selectedOverlays.Contains(_selectedOverlay))
        {
            _selectedOverlay = _selectedOverlays.FirstOrDefault();
        }

        if (SelectionCount == 0)
        {
            if (Preset?.TextBlocks.FirstOrDefault() is { } block)
            {
                SelectTextBlock(block, append: false);
            }
            else
            {
                SelectedTextBlock = null;
                _selectedOverlay = null;
            }
        }
    }

    private int SelectionCount => _selectedTextBlocks.Count + _selectedOverlays.Count;

    public bool CanAlignSelection => SelectionCount > 1;

    private void ClearSelection()
    {
        _selectedTextBlocks.Clear();
        _selectedOverlays.Clear();
        _selectionOrder.Clear();
        SelectedTextBlock = null;
        _selectedOverlay = null;
    }

    private void SelectTextBlock(ModelTextBlock block, bool append)
    {
        if (!append)
        {
            _selectedTextBlocks.Clear();
            _selectedOverlays.Clear();
        }

        _selectedTextBlocks.Add(block);
        TrackSelection(SelectionItemType.TextBlock, block);
        SelectedTextBlock = block;
        _selectedOverlay = null;
    }

    private void SelectOverlay(OverlayItem overlay, bool append)
    {
        if (!append)
        {
            _selectedTextBlocks.Clear();
            _selectedOverlays.Clear();
        }

        _selectedOverlays.Add(overlay);
        TrackSelection(SelectionItemType.Overlay, overlay);
        _selectedOverlay = overlay;
        SelectedTextBlock = null;
    }

    private void UpdateSelectionForTextBlock(ModelTextBlock block, bool allowMultiSelect)
    {
        if (!allowMultiSelect)
        {
            SelectTextBlock(block, append: false);
            return;
        }

        if (!_selectedTextBlocks.Add(block))
        {
            _selectedTextBlocks.Remove(block);
            RemoveTrackedSelection(SelectionItemType.TextBlock, block);
        }
        else
        {
            TrackSelection(SelectionItemType.TextBlock, block);
        }

        if (_selectedTextBlocks.Count == 0 && _selectedOverlays.Count == 0)
        {
            SelectedTextBlock = null;
            return;
        }

        SelectedTextBlock = _selectedTextBlocks.Contains(block) ? block : _selectedTextBlocks.LastOrDefault();
        _selectedOverlay = _selectedOverlays.LastOrDefault();
    }

    private void UpdateSelectionForOverlay(OverlayItem overlay, bool allowMultiSelect)
    {
        if (!allowMultiSelect)
        {
            SelectOverlay(overlay, append: false);
            return;
        }

        if (!_selectedOverlays.Add(overlay))
        {
            _selectedOverlays.Remove(overlay);
            RemoveTrackedSelection(SelectionItemType.Overlay, overlay);
        }
        else
        {
            TrackSelection(SelectionItemType.Overlay, overlay);
        }

        if (_selectedTextBlocks.Count == 0 && _selectedOverlays.Count == 0)
        {
            _selectedOverlay = null;
            SelectedTextBlock = null;
            return;
        }

        _selectedOverlay = _selectedOverlays.Contains(overlay) ? overlay : _selectedOverlays.LastOrDefault();
        if (_selectedOverlay != null)
        {
            SelectedTextBlock = null;
        }
        else
        {
            SelectedTextBlock = _selectedTextBlocks.LastOrDefault();
        }
    }

    private void CaptureDragSelectionSnapshot()
    {
        _dragStartTextPositions.Clear();
        _dragStartOverlayPositions.Clear();

        foreach (var block in _selectedTextBlocks)
        {
            _dragStartTextPositions[block] = new Point(block.TextLayout.OffsetX, block.TextLayout.OffsetY);
        }

        foreach (var overlay in _selectedOverlays)
        {
            _dragStartOverlayPositions[overlay] = new Point(overlay.X, overlay.Y);
        }
    }

    private void MoveSelectedItems(double deltaX, double deltaY)
    {
        foreach (var (block, start) in _dragStartTextPositions)
        {
            block.TextLayout.OffsetX = (float)(start.X + deltaX);
            block.TextLayout.OffsetY = (float)(start.Y + deltaY);
        }

        foreach (var (overlay, start) in _dragStartOverlayPositions)
        {
            overlay.X = (int)Math.Round(start.X + deltaX);
            overlay.Y = (int)Math.Round(start.Y + deltaY);
        }
    }

    private bool TryGetSelectionBounds(out Rect bounds)
    {
        bounds = default;
        var hasBounds = false;

        foreach (var overlay in _selectedOverlays)
        {
            if (_overlayBounds.TryGetValue(overlay, out var overlayBounds))
            {
                bounds = hasBounds ? bounds.Union(overlayBounds) : overlayBounds;
                hasBounds = true;
            }
        }

        foreach (var block in _selectedTextBlocks)
        {
            if (_textBlockBounds.TryGetValue(block, out var textBounds))
            {
                bounds = hasBounds ? bounds.Union(textBounds) : textBounds;
                hasBounds = true;
            }
        }

        return hasBounds;
    }

    private void ApplyRubberBandSelection(Rect selectionRect, bool append)
    {
        UpdateBounds();
        if (!append)
        {
            ClearSelection();
        }

        foreach (var overlay in Preset?.Overlays ?? [])
        {
            if (_overlayBounds.TryGetValue(overlay, out var bounds) && selectionRect.Intersects(bounds))
            {
                _selectedOverlays.Add(overlay);
                TrackSelection(SelectionItemType.Overlay, overlay);
                _selectedOverlay = overlay;
            }
        }

        foreach (var block in Preset?.TextBlocks ?? [])
        {
            if (_textBlockBounds.TryGetValue(block, out var bounds) && selectionRect.Intersects(bounds))
            {
                _selectedTextBlocks.Add(block);
                TrackSelection(SelectionItemType.TextBlock, block);
                SelectedTextBlock = block;
            }
        }

        if (_selectedOverlays.Count > 0)
        {
            SelectedTextBlock = null;
        }
    }

    public bool AlignSelection(SelectionAlignmentCommand command)
    {
        UpdateBounds();
        if (SelectionCount < 2)
        {
            return false;
        }

        var items = GetSelectedItems()
            .Select(CreateAlignmentItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        if (items.Count < 2)
        {
            return false;
        }

        var selectionBounds = items.Select(item => item.Bounds).Aggregate((current, next) => current.Union(next));
        var anchorBounds = AlignmentReferenceMode == SelectionAlignmentReferenceMode.LastSelectedElement
            ? GetLastSelectedBounds() ?? selectionBounds
            : selectionBounds;

        switch (command)
        {
            case SelectionAlignmentCommand.AlignLeft:
                foreach (var item in items)
                {
                    item.SetPosition(anchorBounds.Left, item.Bounds.Top);
                }
                break;
            case SelectionAlignmentCommand.AlignHorizontalCenter:
                foreach (var item in items)
                {
                    item.SetPosition(anchorBounds.Center.X - (item.Bounds.Width / 2d), item.Bounds.Top);
                }
                break;
            case SelectionAlignmentCommand.AlignRight:
                foreach (var item in items)
                {
                    item.SetPosition(anchorBounds.Right - item.Bounds.Width, item.Bounds.Top);
                }
                break;
            case SelectionAlignmentCommand.AlignTop:
                foreach (var item in items)
                {
                    item.SetPosition(item.Bounds.Left, anchorBounds.Top);
                }
                break;
            case SelectionAlignmentCommand.AlignVerticalCenter:
                foreach (var item in items)
                {
                    item.SetPosition(item.Bounds.Left, anchorBounds.Center.Y - (item.Bounds.Height / 2d));
                }
                break;
            case SelectionAlignmentCommand.AlignBottom:
                foreach (var item in items)
                {
                    item.SetPosition(item.Bounds.Left, anchorBounds.Bottom - item.Bounds.Height);
                }
                break;
            case SelectionAlignmentCommand.DistributeHorizontal:
                DistributeItems(items, isHorizontal: true);
                break;
            case SelectionAlignmentCommand.DistributeVertical:
                DistributeItems(items, isHorizontal: false);
                break;
            default:
                return false;
        }

        UpdateBounds();
        InvalidateVisual();
        return true;
    }

    private void DistributeItems(IReadOnlyList<AlignmentItem> items, bool isHorizontal)
    {
        if (items.Count < 3)
        {
            return;
        }

        var ordered = isHorizontal
            ? items.OrderBy(item => item.Bounds.Left).ToList()
            : items.OrderBy(item => item.Bounds.Top).ToList();

        if (AlignmentReferenceMode == SelectionAlignmentReferenceMode.LastSelectedElement
            && TryGetLastSelectedItem(out var anchorEntry))
        {
            var anchorItem = ordered.FirstOrDefault(item => item.Matches(anchorEntry));
            if (anchorItem != null)
            {
                DistributeAroundAnchor(ordered, anchorItem, isHorizontal);
                return;
            }
        }

        DistributeAcrossBounds(ordered, isHorizontal);
    }

    private static void DistributeAcrossBounds(IReadOnlyList<AlignmentItem> ordered, bool isHorizontal)
    {
        var spanStart = isHorizontal ? ordered.First().Bounds.Left : ordered.First().Bounds.Top;
        var spanEnd = isHorizontal ? ordered.Last().Bounds.Right : ordered.Last().Bounds.Bottom;
        var totalSize = ordered.Sum(item => isHorizontal ? item.Bounds.Width : item.Bounds.Height);
        var spacing = (spanEnd - spanStart - totalSize) / (ordered.Count - 1);
        var cursor = spanStart;

        foreach (var item in ordered)
        {
            if (isHorizontal)
            {
                item.SetPosition(cursor, item.Bounds.Top);
                cursor += item.Bounds.Width + spacing;
            }
            else
            {
                item.SetPosition(item.Bounds.Left, cursor);
                cursor += item.Bounds.Height + spacing;
            }
        }
    }

    private static void DistributeAroundAnchor(IReadOnlyList<AlignmentItem> ordered, AlignmentItem anchorItem, bool isHorizontal)
    {
        var anchorIndex = ordered
            .Select((item, index) => new { item, index })
            .FirstOrDefault(x => ReferenceEquals(x.item, anchorItem))
            ?.index ?? -1;
        if (anchorIndex < 0)
        {
            DistributeAcrossBounds(ordered, isHorizontal);
            return;
        }

        var leftItems = ordered.Take(anchorIndex).ToList();
        var rightItems = ordered.Skip(anchorIndex + 1).ToList();

        if (leftItems.Count > 0)
        {
            var start = isHorizontal ? leftItems.First().Bounds.Left : leftItems.First().Bounds.Top;
            var end = isHorizontal ? anchorItem.Bounds.Left : anchorItem.Bounds.Top;
            var totalSize = leftItems.Sum(item => isHorizontal ? item.Bounds.Width : item.Bounds.Height);
            var spacing = (end - start - totalSize) / leftItems.Count;
            var cursor = start;

            foreach (var item in leftItems)
            {
                if (isHorizontal)
                {
                    item.SetPosition(cursor, item.Bounds.Top);
                    cursor += item.Bounds.Width + spacing;
                }
                else
                {
                    item.SetPosition(item.Bounds.Left, cursor);
                    cursor += item.Bounds.Height + spacing;
                }
            }
        }

        if (rightItems.Count > 0)
        {
            var start = isHorizontal ? anchorItem.Bounds.Right : anchorItem.Bounds.Bottom;
            var end = isHorizontal ? rightItems.Last().Bounds.Right : rightItems.Last().Bounds.Bottom;
            var totalSize = rightItems.Sum(item => isHorizontal ? item.Bounds.Width : item.Bounds.Height);
            var spacing = (end - start - totalSize) / rightItems.Count;
            var cursor = start;

            foreach (var item in rightItems)
            {
                if (isHorizontal)
                {
                    item.SetPosition(cursor, item.Bounds.Top);
                    cursor += item.Bounds.Width + spacing;
                }
                else
                {
                    item.SetPosition(item.Bounds.Left, cursor);
                    cursor += item.Bounds.Height + spacing;
                }
            }
        }
    }

    private AlignmentItem? CreateAlignmentItem(SelectionEntry entry)
        => entry.Type switch
        {
            SelectionItemType.TextBlock when entry.Item is ModelTextBlock block && TryGetAlignmentBounds(block, out var blockBounds)
                => new AlignmentItem(entry, blockBounds, (x, y) =>
                {
                    block.TextLayout.OffsetX = (float)x;
                    block.TextLayout.OffsetY = (float)y;
                }),
            SelectionItemType.Overlay when entry.Item is OverlayItem overlay && TryGetAlignmentBounds(overlay, out var overlayBounds)
                => new AlignmentItem(entry, overlayBounds, (x, y) =>
                {
                    overlay.X = (int)Math.Round(x);
                    overlay.Y = (int)Math.Round(y);
                }),
            _ => null
        };

    private Rect? GetLastSelectedBounds()
    {
        if (!TryGetLastSelectedItem(out var entry))
        {
            return null;
        }

        return entry.Type switch
        {
            SelectionItemType.TextBlock when entry.Item is ModelTextBlock block && TryGetAlignmentBounds(block, out var blockBounds) => blockBounds,
            SelectionItemType.Overlay when entry.Item is OverlayItem overlay && TryGetAlignmentBounds(overlay, out var overlayBounds) => overlayBounds,
            _ => null
        };
    }

    private bool TryGetLastSelectedItem(out SelectionEntry entry)
    {
        for (var index = _selectionOrder.Count - 1; index >= 0; index--)
        {
            var candidate = _selectionOrder[index];
            if ((candidate.Type == SelectionItemType.TextBlock && candidate.Item is ModelTextBlock block && _selectedTextBlocks.Contains(block))
                || (candidate.Type == SelectionItemType.Overlay && candidate.Item is OverlayItem overlay && _selectedOverlays.Contains(overlay)))
            {
                entry = candidate;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private void TrackSelection(SelectionItemType type, object item)
    {
        RemoveTrackedSelection(type, item);
        _selectionOrder.Add(new SelectionEntry(type, item));
    }

    private void RemoveTrackedSelection(SelectionItemType type, object item)
    {
        _selectionOrder.RemoveAll(entry => entry.Type == type && ReferenceEquals(entry.Item, item));
    }

    private IEnumerable<SelectionEntry> GetSelectedItems()
    {
        foreach (var entry in _selectionOrder)
        {
            if ((entry.Type == SelectionItemType.TextBlock && entry.Item is ModelTextBlock block && _selectedTextBlocks.Contains(block))
                || (entry.Type == SelectionItemType.Overlay && entry.Item is OverlayItem overlay && _selectedOverlays.Contains(overlay)))
            {
                yield return entry;
            }
        }
    }

    private bool TryGetAlignmentBounds(ModelTextBlock block, out Rect bounds)
    {
        if (_textBlockBounds.TryGetValue(block, out bounds))
        {
            bounds = bounds.Deflate(8);
            return true;
        }

        return false;
    }

    private bool TryGetAlignmentBounds(OverlayItem overlay, out Rect bounds)
    {
        if (_overlayBounds.TryGetValue(overlay, out bounds))
        {
            bounds = bounds.Deflate(6);
            return true;
        }

        return false;
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
        if (_selectedOverlay != null
            && _selectedOverlays.Count == 1
            && _selectedTextBlocks.Count == 0
            && _overlayBounds.TryGetValue(_selectedOverlay, out var selectedBounds)
            && TryHitResizeHandle(selectedBounds.Deflate(6), point, out var resizeHandle))
        {
            return new HitTestResult(DragTargetType.OverlayResize, null, _selectedOverlay, resizeHandle);
        }

        foreach (var overlay in Preset?.Overlays.Reverse() ?? Enumerable.Empty<OverlayItem>())
        {
            if (_overlayBounds.TryGetValue(overlay, out var bounds) && bounds.Contains(point))
            {
                return new HitTestResult(DragTargetType.Overlay, null, overlay, ResizeHandle.None);
            }
        }

        foreach (var block in Preset?.TextBlocks.Reverse() ?? Enumerable.Empty<ModelTextBlock>())
        {
            if (_textBlockBounds.TryGetValue(block, out var bounds) && bounds.Contains(point))
            {
                return new HitTestResult(DragTargetType.TextBlock, block, null, ResizeHandle.None);
            }
        }

        return new HitTestResult(DragTargetType.None, null, null, ResizeHandle.None);
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

    private void ResizeOverlay(OverlayItem overlay, Point point, bool preserveAspectRatio)
    {
        var anchor = GetAnchorPoint(_dragStartOverlayBounds, _activeResizeHandle);
        var newBounds = preserveAspectRatio
            ? CreateAspectLockedRect(anchor, point, _activeResizeHandle, _dragAspectRatio)
            : CreateFreeformRect(anchor, point);

        overlay.X = (int)Math.Round(newBounds.X);
        overlay.Y = (int)Math.Round(newBounds.Y);
        overlay.Width = Math.Max(1, (int)Math.Round(newBounds.Width));
        overlay.Height = Math.Max(1, (int)Math.Round(newBounds.Height));
    }

    public static Rect CreateNormalizedRect(Point start, Point end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(left, top, width, height);
    }

    private static Rect CreateFreeformRect(Point anchor, Point point)
    {
        var left = Math.Min(anchor.X, point.X);
        var top = Math.Min(anchor.Y, point.Y);
        var width = Math.Max(1d, Math.Abs(point.X - anchor.X));
        var height = Math.Max(1d, Math.Abs(point.Y - anchor.Y));
        return new Rect(left, top, width, height);
    }

    private static Rect CreateAspectLockedRect(Point anchor, Point point, ResizeHandle handle, double aspectRatio)
    {
        aspectRatio = aspectRatio <= 0 ? 1d : aspectRatio;
        var deltaX = point.X - anchor.X;
        var deltaY = point.Y - anchor.Y;
        var width = Math.Max(1d, Math.Abs(deltaX));
        var height = Math.Max(1d, Math.Abs(deltaY));

        if (height <= 0.001d || width / height > aspectRatio)
        {
            height = Math.Max(1d, width / aspectRatio);
        }
        else
        {
            width = Math.Max(1d, height * aspectRatio);
        }

        var x = handle is ResizeHandle.TopLeft or ResizeHandle.BottomLeft ? anchor.X - width : anchor.X;
        var y = handle is ResizeHandle.TopLeft or ResizeHandle.TopRight ? anchor.Y - height : anchor.Y;
        return new Rect(x, y, width, height);
    }

    private static Point GetAnchorPoint(Rect bounds, ResizeHandle handle) => handle switch
    {
        ResizeHandle.TopLeft => bounds.BottomRight,
        ResizeHandle.TopRight => bounds.BottomLeft,
        ResizeHandle.BottomLeft => bounds.TopRight,
        ResizeHandle.BottomRight => bounds.TopLeft,
        _ => bounds.TopLeft
    };

    private static bool TryHitResizeHandle(Rect rect, Point point, out ResizeHandle handle)
    {
        foreach (var candidate in EnumerateHandleCenters(rect))
        {
            if (Math.Abs(point.X - candidate.Center.X) <= HandleHitRadius && Math.Abs(point.Y - candidate.Center.Y) <= HandleHitRadius)
            {
                handle = candidate.Handle;
                return true;
            }
        }

        handle = ResizeHandle.None;
        return false;
    }

    private static IEnumerable<(ResizeHandle Handle, Point Center)> EnumerateHandleCenters(Rect rect)
    {
        yield return (ResizeHandle.TopLeft, rect.TopLeft);
        yield return (ResizeHandle.TopRight, rect.TopRight);
        yield return (ResizeHandle.BottomLeft, rect.BottomLeft);
        yield return (ResizeHandle.BottomRight, rect.BottomRight);
    }

    private static Rect GetOverlayRect(OverlayItem overlay) =>
        new(overlay.X, overlay.Y, Math.Max(1, overlay.Width), Math.Max(1, overlay.Height));

    private static double GetOverlayAspectRatio(OverlayItem overlay, Rect overlayBounds)
    {
        var width = Math.Max(1d, overlay.Width > 0 ? overlay.Width : overlayBounds.Width);
        var height = Math.Max(1d, overlay.Height > 0 ? overlay.Height : overlayBounds.Height);
        return width / height;
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
        Overlay,
        Group,
        OverlayResize,
        RubberBand
    }

    private enum ResizeHandle
    {
        None,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    private ResizeHandle _activeResizeHandle;

    private readonly record struct HitTestResult(DragTargetType TargetType, ModelTextBlock? TextBlock, OverlayItem? Overlay, ResizeHandle ResizeHandle);

    private enum SelectionItemType
    {
        TextBlock,
        Overlay
    }

    private readonly record struct SelectionEntry(SelectionItemType Type, object Item);

    private sealed class AlignmentItem(SelectionEntry entry, Rect bounds, Action<double, double> setPosition)
    {
        public SelectionEntry Entry { get; } = entry;
        public Rect Bounds { get; private set; } = bounds;

        public void SetPosition(double x, double y)
        {
            Bounds = new Rect(x, y, Bounds.Width, Bounds.Height);
            setPosition(x, y);
        }

        public bool Matches(SelectionEntry other)
            => Entry.Type == other.Type && ReferenceEquals(Entry.Item, other.Item);
    }
}
