using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using NdiTelop.Models;
using NdiTelop.Services;
using SkiaSharp;
using System;

namespace NdiTelop.Controls;

public partial class PreviewCanvas : UserControl
{
    private readonly RenderService _renderService;
    private SKBitmap? _renderedBitmap;

    public static readonly DirectProperty<PreviewCanvas, Preset?> PresetProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, Preset?>(
            nameof(Preset), o => o.Preset, (o, v) => o.Preset = v);

    private Preset? _preset;
    public Preset? Preset
    {
        get => _preset;
        set
        {
            SetAndRaise(PresetProperty, ref _preset, value);
            InvalidateVisual(); // Request redraw when preset changes
        }
    }

    public PreviewCanvas()
    {
        InitializeComponent();
        _renderService = new RenderService(); // Directly instantiate for simplicity in control
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Preset == null) return;

        // Dispose previous bitmap if exists
        _renderedBitmap?.Dispose();

        // Render the preset to a new bitmap
        _renderedBitmap = _renderService.Render(Preset, (int)Bounds.Width, (int)Bounds.Height);

        if (_renderedBitmap != null)
        {
            var image = new Avalonia.Media.Imaging.Bitmap(_renderedBitmap.Encode(SKEncodedImageFormat.Png, 100).AsStream());
            context.DrawImage(image, new Rect(Bounds.Size));
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual(); // Redraw when size changes
    }
}
