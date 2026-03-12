using NdiTelop.Models;
using NdiTelop.Services;
using SkiaSharp;
using Xunit;

namespace NdiTelop.Tests.Services;

public class RenderServiceTests
{
    [Fact]
    public void Render_ShouldReturnBitmapWithExpectedSize()
    {
        var service = new RenderService();
        var preset = new Preset { TextLines = [new TextLine { Text = "Hello" }] };
        using var bitmap = service.Render(preset, 640, 360);

        Assert.Equal(640, bitmap.Width);
        Assert.Equal(360, bitmap.Height);
        Assert.False(bitmap.Pixels.All(p => p == 0)); // Ensure something was drawn (not completely transparent)
    }

    [Fact]
    public void Render_ShouldDrawBackgroundWhenSpecified()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            Background = new BackgroundStyle { Type = "solid", Color = "#FF0000", Alpha = 1.0 },
            TextLines = []
        };
        using var bitmap = service.Render(preset, 100, 100);

        // Check a pixel to see if it's red (or close to it due to premul alpha)
        var pixel = bitmap.GetPixel(50, 50);
        Assert.Equal(SKColors.Red.Red, pixel.Red);
        Assert.Equal(SKColors.Red.Green, pixel.Green);
        Assert.Equal(SKColors.Red.Blue, pixel.Blue);
        Assert.Equal(SKColors.Red.Alpha, pixel.Alpha);
    }

    [Fact]
    public void Render_ShouldDrawTextWhenSpecified()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            TextLines = [new TextLine { Text = "Test", FontSize = 24, Color = "#FFFFFF" }],
            Background = new BackgroundStyle { Type = "transparent" }
        };
        using var bitmap = service.Render(preset, 200, 100);

        // It's hard to assert exact pixel values for text rendering without a reference image.
        // For now, we'll just ensure the bitmap is not entirely transparent, implying text was drawn.
        // A more robust test would involve image comparison or checking specific pixel regions.
        Assert.False(bitmap.Pixels.All(p => p == 0));
    }

    [Fact]
    public void RenderTransition_ShouldReturnBitmapWithExpectedSize()
    {
        var service = new RenderService();
        var fromPreset = new Preset { TextLines = [new TextLine { Text = "From" }] };
        var toPreset = new Preset { TextLines = [new TextLine { Text = "To" }] };
        var config = new AnimationConfig { InType = "fade" };
        var ndiConfig = new NdiConfig { ResolutionWidth = 1920, ResolutionHeight = 1080 };
        using var bitmap = service.RenderTransition(fromPreset, toPreset, 0.5f, config, ndiConfig);

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);
        Assert.False(bitmap.Pixels.All(p => p == 0));
    }

    [Fact]
    public void Render_WithTransparentBackground_ShouldKeepPixelTransparentWhenNoForeground()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextLines = []
        };

        using var bitmap = service.Render(preset, 64, 64);
        var center = bitmap.GetPixel(32, 32);

        Assert.Equal((byte)0, center.Alpha);
        Assert.Equal((byte)0, center.Red);
        Assert.Equal((byte)0, center.Green);
        Assert.Equal((byte)0, center.Blue);
    }

    [Fact]
    public void Render_WithSemiTransparentBackground_ShouldOutputExpectedAlpha()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            Background = new BackgroundStyle { Type = "solid", Color = "#FF0000", Alpha = 0.5 },
            TextLines = []
        };

        using var bitmap = service.Render(preset, 64, 64);
        var center = bitmap.GetPixel(32, 32);

        Assert.InRange(center.Alpha, (byte)126, (byte)129);
    }

    [Fact]
    public void Render_WithOverlayOpacityOutOfRange_ShouldClampOpacity()
    {
        var service = new RenderService();
        using var overlayBitmap = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var overlayCanvas = new SKCanvas(overlayBitmap))
        {
            overlayCanvas.Clear(SKColors.White);
        }

        var overlayPath = Path.Combine(Path.GetTempPath(), $"overlay-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = SKImage.FromBitmap(overlayBitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(overlayPath))
            {
                data.SaveTo(stream);
            }

            var transparentPreset = new Preset
            {
                Background = new BackgroundStyle { Type = "transparent" },
                TextLines = [],
                Overlays = [new OverlayItem { Path = overlayPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = -0.5 }]
            };

            using var fullyTransparent = service.Render(transparentPreset, 8, 8);
            var transparentPixel = fullyTransparent.GetPixel(2, 2);
            Assert.Equal((byte)0, transparentPixel.Alpha);

            var opaquePreset = new Preset
            {
                Background = new BackgroundStyle { Type = "transparent" },
                TextLines = [],
                Overlays = [new OverlayItem { Path = overlayPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = 2.0 }]
            };

            using var opaque = service.Render(opaquePreset, 8, 8);
            var opaquePixel = opaque.GetPixel(2, 2);
            Assert.Equal((byte)255, opaquePixel.Alpha);
        }
        finally
        {
            if (File.Exists(overlayPath))
            {
                File.Delete(overlayPath);
            }
        }
    }


    [Fact]
    public void Render_WithInvisibleOverlay_ShouldNotDrawOverlay()
    {
        var service = new RenderService();
        using var overlayBitmap = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var overlayCanvas = new SKCanvas(overlayBitmap))
        {
            overlayCanvas.Clear(SKColors.White);
        }

        var overlayPath = Path.Combine(Path.GetTempPath(), $"overlay-hidden-{Guid.NewGuid():N}.png");
        try
        {
            using (var image = SKImage.FromBitmap(overlayBitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(overlayPath))
            {
                data.SaveTo(stream);
            }

            var preset = new Preset
            {
                Background = new BackgroundStyle { Type = "transparent" },
                TextLines = [],
                Overlays = [new OverlayItem { Path = overlayPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = 1.0, IsVisible = false }]
            };

            using var bitmap = service.Render(preset, 8, 8);
            var pixel = bitmap.GetPixel(2, 2);

            Assert.Equal((byte)0, pixel.Alpha);
        }
        finally
        {
            if (File.Exists(overlayPath))
            {
                File.Delete(overlayPath);
            }
        }
    }

}