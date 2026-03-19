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


    [Theory]
    [InlineData("wipe")]
    [InlineData("wipe-vertical")]
    [InlineData("zoom")]
    public void RenderTransition_ShouldSupportAdditionalTransitionTypes(string transitionType)
    {
        var service = new RenderService();
        var fromPreset = new Preset
        {
            TextLines = [],
            Background = new BackgroundStyle { Type = "solid", Color = "#0000FF", Alpha = 1.0 }
        };
        var toPreset = new Preset
        {
            TextLines = [],
            Background = new BackgroundStyle { Type = "solid", Color = "#00FF00", Alpha = 1.0 }
        };

        var config = new AnimationConfig { InType = transitionType };
        var ndiConfig = new NdiConfig { ResolutionWidth = 320, ResolutionHeight = 180 };

        using var bitmap = service.RenderTransition(fromPreset, toPreset, 0.5f, config, ndiConfig);

        Assert.Equal(320, bitmap.Width);
        Assert.Equal(180, bitmap.Height);
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

    [Fact]
    public void Render_WithSemiTransparentOverlayAndOpacity_ShouldMultiplyAlpha()
    {
        var service = new RenderService();
        using var overlayBitmap = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var overlayCanvas = new SKCanvas(overlayBitmap))
        {
            overlayCanvas.Clear(new SKColor(255, 0, 0, 128));
        }

        var overlayPath = Path.Combine(Path.GetTempPath(), $"overlay-alpha-{Guid.NewGuid():N}.png");
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
                Overlays = [new OverlayItem { Path = overlayPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = 0.5 }]
            };

            using var bitmap = service.Render(preset, 8, 8);
            var pixel = bitmap.GetPixel(2, 2);

            Assert.InRange(pixel.Alpha, (byte)63, (byte)65);
            Assert.Equal((byte)255, pixel.Red);
            Assert.Equal((byte)0, pixel.Green);
            Assert.Equal((byte)0, pixel.Blue);
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
    public void Render_WithOverlayWidthAndHeight_ShouldScaleOverlayToRequestedSize()
    {
        var service = new RenderService();
        using var overlayBitmap = new SKBitmap(2, 2, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var overlayCanvas = new SKCanvas(overlayBitmap))
        {
            overlayCanvas.Clear(SKColors.White);
        }

        var overlayPath = Path.Combine(Path.GetTempPath(), $"overlay-scale-{Guid.NewGuid():N}.png");
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
                Overlays = [new OverlayItem { Path = overlayPath, X = 1, Y = 1, Width = 4, Height = 3, Opacity = 1.0 }]
            };

            using var bitmap = service.Render(preset, 8, 8);

            Assert.Equal((byte)255, bitmap.GetPixel(4, 3).Alpha);
            Assert.Equal((byte)0, bitmap.GetPixel(5, 5).Alpha);
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
    public void Render_WithTransparentOverlaysStacked_ShouldKeepExpectedColorBalance()
    {
        var service = new RenderService();

        using var redOverlayBitmap = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var redCanvas = new SKCanvas(redOverlayBitmap))
        {
            redCanvas.Clear(new SKColor(255, 0, 0, 128));
        }

        using var greenOverlayBitmap = new SKBitmap(4, 4, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var greenCanvas = new SKCanvas(greenOverlayBitmap))
        {
            greenCanvas.Clear(new SKColor(0, 255, 0, 128));
        }

        var redPath = Path.Combine(Path.GetTempPath(), $"overlay-red-{Guid.NewGuid():N}.png");
        var greenPath = Path.Combine(Path.GetTempPath(), $"overlay-green-{Guid.NewGuid():N}.png");

        try
        {
            using (var redImage = SKImage.FromBitmap(redOverlayBitmap))
            using (var redData = redImage.Encode(SKEncodedImageFormat.Png, 100))
            using (var redStream = File.OpenWrite(redPath))
            {
                redData.SaveTo(redStream);
            }

            using (var greenImage = SKImage.FromBitmap(greenOverlayBitmap))
            using (var greenData = greenImage.Encode(SKEncodedImageFormat.Png, 100))
            using (var greenStream = File.OpenWrite(greenPath))
            {
                greenData.SaveTo(greenStream);
            }

            var preset = new Preset
            {
                Background = new BackgroundStyle { Type = "transparent" },
                TextLines = [],
                Overlays =
                [
                    new OverlayItem { Path = redPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = 0.5 },
                    new OverlayItem { Path = greenPath, X = 1, Y = 1, Width = 2, Height = 2, Opacity = 0.5 }
                ]
            };

            using var bitmap = service.Render(preset, 8, 8);
            var pixel = bitmap.GetPixel(2, 2);

            Assert.InRange(pixel.Alpha, (byte)111, (byte)113);
            Assert.InRange(pixel.Red, (byte)108, (byte)110);
            Assert.InRange(pixel.Green, (byte)145, (byte)147);
            Assert.Equal((byte)0, pixel.Blue);
        }
        finally
        {
            if (File.Exists(redPath))
            {
                File.Delete(redPath);
            }

            if (File.Exists(greenPath))
            {
                File.Delete(greenPath);
            }
        }
    }


    [Fact]
    public void Render_ShouldApplyPresetTextStyleOverridesIncludingShadowAndOutline()
    {
        var service = new RenderService();
        var basePreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextLines = [new TextLine { Text = "Style", FontFamily = "Meiryo", FontSize = 24, Color = "#FFFFFF" }]
        };

        var styledPreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextStyle = new TextStyleSettings
            {
                FontFamily = "Arial",
                FontSize = 48,
                Color = "#FF0000",
                OutlineThickness = 3,
                OutlineColor = "#00FF00",
                ShadowOffsetX = 6,
                ShadowOffsetY = 6,
                ShadowBlur = 2,
                ShadowColor = "#800000FF"
            },
            TextLines = [new TextLine { Text = "Style", FontFamily = "Meiryo", FontSize = 24, Color = "#FFFFFF" }]
        };

        using var baseBitmap = service.Render(basePreset, 320, 180);
        using var styledBitmap = service.Render(styledPreset, 320, 180);

        Assert.NotEmpty(baseBitmap.Bytes);
        Assert.NotEmpty(styledBitmap.Bytes);

        var hasDifference = false;
        for (var y = 0; y < styledBitmap.Height && !hasDifference; y++)
        {
            for (var x = 0; x < styledBitmap.Width; x++)
            {
                if (styledBitmap.GetPixel(x, y) != baseBitmap.GetPixel(x, y))
                {
                    hasDifference = true;
                    break;
                }
            }
        }

        Assert.True(hasDifference);
    }


    [Fact]
    public void Render_ShouldRespectHorizontalAlignmentAndOffsets()
    {
        var service = new RenderService();
        var leftPreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextStyle = new TextStyleSettings { FontSize = 64, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Left, OffsetX = 20 },
            TextLines = [new TextLine { Text = "Align" }]
        };

        var rightPreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextStyle = new TextStyleSettings { FontSize = 64, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Right, OffsetX = -20 },
            TextLines = [new TextLine { Text = "Align" }]
        };

        using var leftBitmap = service.Render(leftPreset, 400, 200);
        using var rightBitmap = service.Render(rightPreset, 400, 200);

        var leftBounds = GetDrawnBounds(leftBitmap);
        var rightBounds = GetDrawnBounds(rightBitmap);

        Assert.NotNull(leftBounds);
        Assert.NotNull(rightBounds);
        Assert.True(leftBounds!.Value.Left < rightBounds!.Value.Left);
        Assert.True(leftBounds.Value.Right < rightBounds.Value.Right);
    }

    [Fact]
    public void Render_ShouldRespectVerticalAlignmentAndOffsets()
    {
        var service = new RenderService();
        var topPreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextStyle = new TextStyleSettings { FontSize = 56, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings { VerticalAlignment = VerticalTextAlignment.Top, OffsetY = 12 },
            TextLines = [new TextLine { Text = "Top" }]
        };

        var bottomPreset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextStyle = new TextStyleSettings { FontSize = 56, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings { VerticalAlignment = VerticalTextAlignment.Bottom, OffsetY = -12 },
            TextLines = [new TextLine { Text = "Bottom" }]
        };

        using var topBitmap = service.Render(topPreset, 400, 220);
        using var bottomBitmap = service.Render(bottomPreset, 400, 220);

        var topBounds = GetDrawnBounds(topBitmap);
        var bottomBounds = GetDrawnBounds(bottomBitmap);

        Assert.NotNull(topBounds);
        Assert.NotNull(bottomBounds);
        Assert.True(topBounds!.Value.Top < bottomBounds!.Value.Top);
        Assert.True(topBounds.Value.Bottom < bottomBounds.Value.Bottom);
    }

    [Fact]
    public void Render_ShouldDrawMultipleTextBlocksAtIndependentPositions()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            Background = new BackgroundStyle { Type = "transparent" },
            TextBlocks =
            [
                new NdiTelop.Models.TextBlock
                {
                    Name = "TopLeft",
                    TextStyle = new TextStyleSettings { FontSize = 48, Color = "#FFFFFF" },
                    TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Left, VerticalAlignment = VerticalTextAlignment.Top, OffsetX = 10, OffsetY = 10 },
                    TextLines = [new TextLine { Text = "A" }]
                },
                new NdiTelop.Models.TextBlock
                {
                    Name = "BottomRight",
                    TextStyle = new TextStyleSettings { FontSize = 48, Color = "#FFFFFF" },
                    TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Right, VerticalAlignment = VerticalTextAlignment.Bottom, OffsetX = -10, OffsetY = -10 },
                    TextLines = [new TextLine { Text = "B" }]
                }
            ]
        };

        using var bitmap = service.Render(preset, 400, 200);
        var bounds = GetDrawnBounds(bitmap);

        Assert.NotNull(bounds);
        Assert.True(bounds!.Value.Left < 80);
        Assert.True(bounds.Value.Top < 80);
        Assert.True(bounds.Value.Right > 320);
        Assert.True(bounds.Value.Bottom > 120);
    }

    private static SKRectI? GetDrawnBounds(SKBitmap bitmap)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).Alpha == 0)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return maxX >= 0 ? new SKRectI(minX, minY, maxX, maxY) : null;
    }

}
