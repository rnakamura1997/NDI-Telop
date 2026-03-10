using Xunit;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using SkiaSharp;
using System.Threading.Tasks;
using NewTek.NDI;
using System;

namespace NdiTelop.Tests.Services;

public class NdiServiceTests : IDisposable
{
    private NdiService _ndiService;
    private NdiConfig _defaultNdiConfig;

    public NdiServiceTests()
    {
        _ndiService = new NdiService();
        _defaultNdiConfig = new NdiConfig
        {
            SourceName = "Test NDI Source",
            ResolutionWidth = 1920,
            ResolutionHeight = 1080,
            FrameRateN = 30000,
            FrameRateD = 1001
        };
    }

    public void Dispose()
    {
        _ndiService.Dispose();
    }

    [Fact(Skip = "Requires NDI runtime to be installed.")]
    public async Task InitializeAsync_ShouldSetIsInitializedToTrue()
    {
        Assert.False(_ndiService.IsInitialized);
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        Assert.True(_ndiService.IsInitialized);
    }

    [Fact(Skip = "Requires NDI runtime to be installed.")]
    public async Task InitializeAsync_ShouldNotReinitialize()
    {
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        var initialInitializedState = _ndiService.IsInitialized;
        await _ndiService.InitializeAsync(_defaultNdiConfig); // Call again
        Assert.Equal(initialInitializedState, _ndiService.IsInitialized); // Should still be initialized
    }

    [Fact(Skip = "Requires NDI runtime to be installed.")]
    public async Task SetActiveAsync_ShouldSetProgramActive()
    {
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        Assert.False(_ndiService.IsProgramActive);
        await _ndiService.SetActiveAsync(NdiTelop.Models.NdiChannelType.Program, true);
        Assert.True(_ndiService.IsProgramActive);
        await _ndiService.SetActiveAsync(NdiTelop.Models.NdiChannelType.Program, false);
        Assert.False(_ndiService.IsProgramActive);
    }

    [Fact(Skip = "Requires NDI runtime to be installed.")]
    public async Task SetActiveAsync_ShouldSetPreviewActive()
    {
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        Assert.False(_ndiService.IsPreviewActive);
        await _ndiService.SetActiveAsync(NdiTelop.Models.NdiChannelType.Preview, true);
        Assert.True(_ndiService.IsPreviewActive);
        await _ndiService.SetActiveAsync(NdiTelop.Models.NdiChannelType.Preview, false);
        Assert.False(_ndiService.IsPreviewActive);
    }

    [Fact(Skip = "Requires NDI runtime to be installed and a valid NDI source.")]
    public async Task SendFrameAsync_ShouldSendFrameWithoutError()
    {
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        await _ndiService.SetActiveAsync(NdiTelop.Models.NdiChannelType.Program, true);

        using var bitmap = new SKBitmap(_defaultNdiConfig.ResolutionWidth, _defaultNdiConfig.ResolutionHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);

        // This test primarily checks if the method call itself throws an exception.
        // Verifying actual NDI output requires external tools.
        var exception = await Record.ExceptionAsync(() => _ndiService.SendFrameAsync(NdiTelop.Models.NdiChannelType.Program, bitmap));
        Assert.Null(exception);
    }

    [Fact(Skip = "Requires NDI runtime to be installed.")]
    public async Task Dispose_ShouldCleanUpResources()
    {
        await _ndiService.InitializeAsync(_defaultNdiConfig);
        // NdiSend object is internal, so we can't directly assert its disposal.
        // We rely on the Dispose method to be called and not throw exceptions.
        var exception = Record.Exception(() => _ndiService.Dispose());
        // NDIlib.destroy() が呼ばれたことを検証するモックがないため、IsInitialized が false になることを確認する
        Assert.Null(exception);
        Assert.False(_ndiService.IsInitialized); // Should be false after dispose
    }
}
