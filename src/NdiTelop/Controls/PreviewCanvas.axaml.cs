using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Skia;
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

    public static readonly DirectProperty<PreviewCanvas, NdiConfig?> NdiConfigProperty =
        AvaloniaProperty.RegisterDirect<PreviewCanvas, NdiConfig?>(
            nameof(NdiConfig), o => o.NdiConfig, (o, v) => o.NdiConfig = v);

    private NdiConfig? _ndiConfig;
    public NdiConfig? NdiConfig
    {
        get => _ndiConfig;
        set
        {
            SetAndRaise(NdiConfigProperty, ref _ndiConfig, value);
            InvalidateVisual(); // Request redraw when NDI config changes
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

        if (Preset == null || NdiConfig == null) return;

        // Dispose previous bitmap if exists
        _renderedBitmap?.Dispose();

        // Render the preset to a new bitmap using NDI config resolution
        _renderedBitmap = _renderService.Render(Preset, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight);

        if (_renderedBitmap != null)
        {
            // Scale the rendered bitmap to fit the control's bounds for display
            var scaleMatrix = Matrix.CreateScale(Bounds.Width / NdiConfig.ResolutionWidth, Bounds.Height / NdiConfig.ResolutionHeight);
            using (context.PushTransform(scaleMatrix))
            {
                var image = new Avalonia.Media.Imaging.Bitmap(_renderedBitmap.Encode(SKEncodedImageFormat.Png, 100).AsStream());
                context.DrawImage(image, new Rect(0, 0, NdiConfig.ResolutionWidth, NdiConfig.ResolutionHeight));
            }
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        InvalidateVisual(); // Redraw when size changes
    }
}
