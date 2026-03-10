using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

public class NdiService : INdiService
{
    public bool IsInitialized { get; private set; }
    public bool IsProgramActive { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public Task InitializeAsync(NdiConfig config)
    {
        IsInitialized = true;
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(NdiChannelType channel, SKBitmap frame)
    {
        return Task.CompletedTask;
    }

    public Task SetActiveAsync(NdiChannelType channel, bool active)
    {
        if (channel == NdiChannelType.Program) IsProgramActive = active;
        if (channel == NdiChannelType.Preview) IsPreviewActive = active;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
    }
}
