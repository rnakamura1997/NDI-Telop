using NdiTelop.Interfaces;
using NdiTelop.Models;
using NewTek.NDI;
using Serilog;
using SkiaSharp;
using static NewTek.NDIlib;

namespace NdiTelop.Services;

public class NdiService : INdiService
{
    private NewTek.NDI.Sender? _ndiSender;
    private NdiConfig? _ndiConfig;

    public bool IsInitialized => _ndiSender != null;
    public bool IsProgramActive { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public async Task InitializeAsync(NdiConfig config)
    {
        if (IsInitialized)
        {
            Log.Debug("NDI initialize requested while already initialized.");
            return;
        }

        if (!IsRuntimeSupported())
        {
            Log.Error("NDI runtime is not supported or not installed.");
            throw new InvalidOperationException("NDI runtime is not supported or not installed.");
        }

        try
        {
            _ndiConfig = config;
            _ndiSender = new NewTek.NDI.Sender(config.SourceName, true, false, Array.Empty<string>());
            Log.Information("NDI initialized successfully. Source={SourceName}, Resolution={Width}x{Height}, Framerate={Numerator}/{Denominator}",
                config.SourceName,
                config.ResolutionWidth,
                config.ResolutionHeight,
                config.FrameRateN,
                config.FrameRateD);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "NDI initialization failed. Source={SourceName}", config.SourceName);
            throw;
        }

        await Task.CompletedTask;
    }

    public async Task SendFrameAsync(NdiChannelType channel, SKBitmap frame)
    {
        if (!IsInitialized || _ndiSender == null || _ndiConfig == null) return;
        if (channel == NdiChannelType.Program && !IsProgramActive) return;
        if (channel == NdiChannelType.Preview && !IsPreviewActive) return;

        var info = frame.Info;
        if (info.Width != _ndiConfig.ResolutionWidth || info.Height != _ndiConfig.ResolutionHeight)
        {
            Log.Warning("Skipped NDI frame due to size mismatch. Expected={ExpectedWidth}x{ExpectedHeight}, Actual={ActualWidth}x{ActualHeight}",
                _ndiConfig.ResolutionWidth,
                _ndiConfig.ResolutionHeight,
                info.Width,
                info.Height);
            return;
        }

        using var videoFrame = new NewTek.NDI.VideoFrame(
            frame.GetPixels(),
            _ndiConfig.ResolutionWidth,
            _ndiConfig.ResolutionHeight,
            frame.RowBytes,
            FourCC_type_e.FourCC_type_BGRA,
            (float)_ndiConfig.ResolutionWidth / _ndiConfig.ResolutionHeight,
            _ndiConfig.FrameRateN,
            _ndiConfig.FrameRateD,
            frame_format_type_e.frame_format_type_progressive
        );

        _ndiSender.Send(videoFrame);
        await Task.CompletedTask;
    }

    public async Task SetActiveAsync(NdiChannelType channel, bool active)
    {
        if (!IsInitialized) return;

        if (channel == NdiChannelType.Program) IsProgramActive = active;
        else if (channel == NdiChannelType.Preview) IsPreviewActive = active;

        Log.Information("NDI channel status changed. Channel={Channel}, Active={Active}", channel, active);
        await Task.CompletedTask;
    }

    private static bool IsRuntimeSupported()
    {
        var isSupportedProperty = typeof(NewTek.NDIlib).GetProperty("IsSupported", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (isSupportedProperty?.GetValue(null) is bool isSupported)
        {
            return isSupported;
        }

        return true;
    }

    public void Dispose()
    {
        _ndiSender?.Dispose();
        Log.Information("NDI sender disposed.");
    }
}
