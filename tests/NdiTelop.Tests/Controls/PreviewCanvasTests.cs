using Xunit;
using Avalonia;
using NdiTelop.Controls;
using NdiTelop.Models;
using System.Reflection;
using System.Linq;

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


    [Fact]
    public void MoveSelectionToKeyer_ShouldReassignSelectedOverlayAndTextBlock()
    {
        var overlay = new OverlayItem { X = 10, Y = 20, Width = 30, Height = 40, DestinationKeyer = KeyerDestination.Usk1 };
        var block = new TextBlock
        {
            Name = "Title",
            DestinationKeyer = KeyerDestination.Usk1,
            TextStyle = new TextStyleSettings { FontSize = 32, Color = "#FFFFFF" },
            TextLayout = new TextLayoutSettings(),
            TextLines = [new TextLine { Text = "Hello", FontSize = 32, Color = "#FFFFFF" }]
        };

        var preset = new Preset();
        preset.EnsureTextBlocksInitialized();
        preset.GetKeyer(KeyerDestination.Usk1).KeyOn = true;
        preset.GetKeyer(KeyerDestination.Dsk1).KeyOn = true;
        preset.GetKeyer(KeyerDestination.Usk1).Overlays.Add(overlay);
        preset.GetKeyer(KeyerDestination.Usk1).TextBlocks.Add(block);

        var canvas = new PreviewCanvas
        {
            Preset = preset,
            NdiConfig = new NdiConfig { ResolutionWidth = 1920, ResolutionHeight = 1080 }
        };

        SelectOverlay(canvas, overlay, append: false);
        SelectTextBlock(canvas, block, append: true);

        var moved = canvas.MoveSelectionToKeyer(KeyerDestination.Dsk1);

        Assert.Equal(2, moved);
        Assert.Equal(KeyerDestination.Dsk1, overlay.DestinationKeyer);
        Assert.Equal(KeyerDestination.Dsk1, block.DestinationKeyer);
        Assert.Contains(overlay, preset.GetKeyer(KeyerDestination.Dsk1).Overlays);
        Assert.Contains(block, preset.GetKeyer(KeyerDestination.Dsk1).TextBlocks);
    }

    [Fact]
    public void SelectAllInKeyer_ShouldSelectAllVisibleElementsInTargetKeyer()
    {
        var first = new OverlayItem { X = 0, Y = 0, Width = 40, Height = 20, DestinationKeyer = KeyerDestination.Usk2, IsVisible = true };
        var second = new OverlayItem { X = 50, Y = 0, Width = 40, Height = 20, DestinationKeyer = KeyerDestination.Usk2, IsVisible = true };
        var hidden = new OverlayItem { X = 100, Y = 0, Width = 40, Height = 20, DestinationKeyer = KeyerDestination.Usk2, IsVisible = false };
        var canvas = CreateCanvasWithOverlays(first, second, hidden);
        canvas.Preset!.GetKeyer(KeyerDestination.Usk1).KeyOn = false;
        canvas.Preset!.GetKeyer(KeyerDestination.Usk2).KeyOn = true;
        foreach (var overlay in canvas.Preset.GetKeyer(KeyerDestination.Usk1).Overlays.ToList())
        {
            canvas.Preset.GetKeyer(KeyerDestination.Usk1).Overlays.Remove(overlay);
            canvas.Preset.GetKeyer(KeyerDestination.Usk2).Overlays.Add(overlay);
            overlay.DestinationKeyer = KeyerDestination.Usk2;
        }

        canvas.SelectAllInKeyer(KeyerDestination.Usk2);

        Assert.True(canvas.HasSelection);
        Assert.Equal(2, canvas.MoveSelectionToKeyer(KeyerDestination.Dsk2));
        Assert.All(canvas.Preset.GetKeyer(KeyerDestination.Dsk2).Overlays, x => Assert.Equal(KeyerDestination.Dsk2, x.DestinationKeyer));
        Assert.DoesNotContain(hidden, canvas.Preset.GetKeyer(KeyerDestination.Dsk2).Overlays);
    }

    [Fact]
    public void SoloKeyerDestination_ShouldLimitInteractiveSelectionScope()
    {
        var first = new OverlayItem { X = 0, Y = 0, Width = 40, Height = 20, DestinationKeyer = KeyerDestination.Usk1 };
        var second = new OverlayItem { X = 50, Y = 0, Width = 40, Height = 20, DestinationKeyer = KeyerDestination.Dsk1 };
        var preset = new Preset();
        preset.EnsureTextBlocksInitialized();
        preset.GetKeyer(KeyerDestination.Usk1).KeyOn = true;
        preset.GetKeyer(KeyerDestination.Dsk1).KeyOn = true;
        preset.GetKeyer(KeyerDestination.Usk1).Overlays.Add(first);
        preset.GetKeyer(KeyerDestination.Dsk1).Overlays.Add(second);
        var canvas = new PreviewCanvas
        {
            Preset = preset,
            NdiConfig = new NdiConfig { ResolutionWidth = 1920, ResolutionHeight = 1080 }
        };

        canvas.SoloKeyerDestination = KeyerDestination.Dsk1;
        canvas.SelectAllInKeyer(KeyerDestination.Usk1);

        Assert.False(canvas.HasSelection);

        canvas.SelectAllInKeyer(KeyerDestination.Dsk1);

        Assert.True(canvas.HasSelection);
        Assert.Equal(1, canvas.MoveSelectionToKeyer(KeyerDestination.Dsk2));
    }

    private static PreviewCanvas CreateCanvasWithOverlays(params OverlayItem[] overlays)
    {
        var preset = new Preset();
        preset.EnsureTextBlocksInitialized();
        var keyer = preset.GetKeyer(KeyerDestination.Usk1);
        keyer.KeyOn = true;
        foreach (var overlay in overlays)
        {
            overlay.DestinationKeyer = KeyerDestination.Usk1;
            keyer.Overlays.Add(overlay);
        }

        return new PreviewCanvas
        {
            Preset = preset,
            NdiConfig = new NdiConfig
            {
                ResolutionWidth = 1920,
                ResolutionHeight = 1080
            }
        };
    }

    private static void SelectOverlay(PreviewCanvas canvas, OverlayItem overlay, bool append)
    {
        var method = typeof(PreviewCanvas).GetMethod("SelectOverlay", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, [overlay, append]);
    }
    private static void SelectTextBlock(PreviewCanvas canvas, TextBlock block, bool append)
    {
        var method = typeof(PreviewCanvas).GetMethod("SelectTextBlock", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method!.Invoke(canvas, [block, append]);
    }
}
