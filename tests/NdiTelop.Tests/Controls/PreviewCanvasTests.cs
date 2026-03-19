using Xunit;
using Avalonia;
using NdiTelop.Controls;
using NdiTelop.Models;
using System.Reflection;

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

    [Fact]
    public void AlignSelection_AlignLeft_UsesSelectionBounds()
    {
        var first = new OverlayItem { X = 120, Y = 40, Width = 60, Height = 30 };
        var second = new OverlayItem { X = 240, Y = 120, Width = 50, Height = 20 };
        var canvas = CreateCanvasWithOverlays(first, second);

        SelectOverlay(canvas, first, append: false);
        SelectOverlay(canvas, second, append: true);

        var applied = canvas.AlignSelection(SelectionAlignmentCommand.AlignLeft);

        Assert.True(applied);
        Assert.Equal(first.X, second.X);
        Assert.Equal(120, first.X);
    }

    [Fact]
    public void AlignSelection_AlignLeft_UsesLastSelectedOverlayAsAnchor()
    {
        var first = new OverlayItem { X = 120, Y = 40, Width = 60, Height = 30 };
        var second = new OverlayItem { X = 240, Y = 120, Width = 50, Height = 20 };
        var canvas = CreateCanvasWithOverlays(first, second);
        canvas.AlignmentReferenceMode = SelectionAlignmentReferenceMode.LastSelectedElement;

        SelectOverlay(canvas, first, append: false);
        SelectOverlay(canvas, second, append: true);

        var applied = canvas.AlignSelection(SelectionAlignmentCommand.AlignLeft);

        Assert.True(applied);
        Assert.Equal(240, first.X);
        Assert.Equal(240, second.X);
    }

    [Fact]
    public void AlignSelection_DistributeHorizontal_SpreadsOverlaysEvenly()
    {
        var first = new OverlayItem { X = 0, Y = 0, Width = 40, Height = 20 };
        var second = new OverlayItem { X = 100, Y = 0, Width = 40, Height = 20 };
        var third = new OverlayItem { X = 300, Y = 0, Width = 40, Height = 20 };
        var canvas = CreateCanvasWithOverlays(first, second, third);

        SelectOverlay(canvas, first, append: false);
        SelectOverlay(canvas, second, append: true);
        SelectOverlay(canvas, third, append: true);

        var applied = canvas.AlignSelection(SelectionAlignmentCommand.DistributeHorizontal);

        Assert.True(applied);
        Assert.Equal(0, first.X);
        Assert.Equal(150, second.X);
        Assert.Equal(300, third.X);
    }

    private static PreviewCanvas CreateCanvasWithOverlays(params OverlayItem[] overlays)
        => new()
        {
            Preset = new Preset
            {
                Overlays = [.. overlays],
                TextBlocks = []
            },
            NdiConfig = new NdiConfig
            {
                ResolutionWidth = 1920,
                ResolutionHeight = 1080
            }
        };

    private static void SelectOverlay(PreviewCanvas canvas, OverlayItem overlay, bool append)
    {
        var method = typeof(PreviewCanvas).GetMethod("SelectOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, [overlay, append]);
    }
}
