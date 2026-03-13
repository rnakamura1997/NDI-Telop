using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Interfaces;

public interface INdiService : IDisposable
{
    bool IsInitialized { get; }
    bool IsProgramActive { get; }
    bool IsPreviewActive { get; }
    Task InitializeAsync(NdiConfig config);
    Task ReinitializeAsync(NdiConfig config);
    Task SendFrameAsync(NdiChannelType channel, SKBitmap frame);
    Task SetActiveAsync(NdiChannelType channel, bool active);
}
