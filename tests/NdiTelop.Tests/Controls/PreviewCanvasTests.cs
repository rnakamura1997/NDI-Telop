using Xunit;
using Avalonia;
using NdiTelop.Controls;

namespace NdiTelop.Tests.Controls;

public class PreviewCanvasTests
{
    [Fact]
    public void CreateNormalizedRect_NormalizesReverseDragCoordinates()
    {
        var rect = PreviewCanvas.CreateNormalizedRect(new Point(120, 80), new Point(20, 30));

        Assert.Equal(20, rect.X);
        Assert.Equal(30, rect.Y);
        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
    }

    [Fact]
    public void CreateNormalizedRect_ReturnsEmptyAreaForClickWithoutMovement()
    {
        var rect = PreviewCanvas.CreateNormalizedRect(new Point(42, 24), new Point(42, 24));

        Assert.Equal(42, rect.X);
        Assert.Equal(24, rect.Y);
        Assert.Equal(0, rect.Width);
        Assert.Equal(0, rect.Height);
    }
}
