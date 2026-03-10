using NdiTelop.Models;
using NdiTelop.Services;
using SkiaSharp;
using Xunit;

namespace NdiTelop.Tests.Services;

public class NdiServiceTests
{
    [Fact]
    public async Task InitializeAsync_ShouldSetInitialized()
    {
        var service = new NdiService();

        await service.InitializeAsync(new NdiConfig());

        Assert.True(service.IsInitialized);
        Assert.Contains("initialized", service.LastStatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TakePreviewToProgramAsync_ShouldReturnFalse_WhenPreviewIsEmpty()
    {
        var service = new NdiService();

        var result = await service.TakePreviewToProgramAsync();

        Assert.False(result);
        Assert.Contains("preview", service.LastStatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendFrameAsync_ShouldAutoInitializeAndActivatePreview()
    {
        var service = new NdiService();
        using var bitmap = new SKBitmap(320, 180);

        await service.SendFrameAsync(NdiChannelType.Preview, bitmap);

        Assert.True(service.IsInitialized);
        Assert.True(service.IsPreviewActive);
    }
}
