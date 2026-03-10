using NdiTelop.Models;
using NdiTelop.Services;
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
    }
}
