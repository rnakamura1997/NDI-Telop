using NdiTelop.Models;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class RenderServiceTests
{
    [Fact]
    public void Render_ShouldCreateBitmapWithoutException()
    {
        var service = new RenderService();
        var preset = new Preset
        {
            Name = "test",
            TextLines = [new TextLine { Text = "Hello", FontSize = 48, Color = "#FFFFFF" }],
            Background = new BackgroundStyle { Type = "solid", Color = "#000000", Alpha = 0.5 }
        };

        using var bitmap = service.Render(preset, 1280, 720);

        Assert.Equal(1280, bitmap.Width);
        Assert.Equal(720, bitmap.Height);
    }

    [Fact]
    public void RenderTransition_Fade_ShouldCreateBitmap()
    {
        var service = new RenderService();
        var from = new Preset { Name = "from", TextLines = [new TextLine { Text = "A" }] };
        var to = new Preset { Name = "to", TextLines = [new TextLine { Text = "B" }] };

        using var bitmap = service.RenderTransition(from, to, 0.5f, new AnimationConfig { InType = "fade" });

        Assert.Equal(1920, bitmap.Width);
        Assert.Equal(1080, bitmap.Height);
    }
}
