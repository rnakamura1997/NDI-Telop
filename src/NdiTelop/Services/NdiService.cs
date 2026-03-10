using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;
using System.Runtime.InteropServices;
using NewTek.NDI;
using NewTek.NDIlib;

namespace NdiTelop.Services;

public class NdiService : INdiService
{
    private NewTek.NDI.Sender? _ndiSender;
    private NdiConfig? _ndiConfig;

    public bool IsInitialized => _ndiSender != null;
    public bool IsProgramActive { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public NdiService()
    {
    }

    public async Task InitializeAsync(NdiConfig config)
    {
        if (IsInitialized) return;

        if (!NDIlib.IsSupported)
        {
            throw new InvalidOperationException("NDI runtime is not supported or not installed.");
        }

        _ndiConfig = config;
        _ndiSender = new NewTek.NDI.Sender(config.SourceName, true, false, Array.Empty<string>());

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
            return;
        }

        using var videoFrame = new NewTek.NDI.VideoFrame(
            frame.GetPixels(), // IntPtr bufferPtr
            _ndiConfig.ResolutionWidth, // int width
            _ndiConfig.ResolutionHeight, // int height
            frame.RowBytes, // int stride
            NDIlib.FourCC_type_e.FourCC_type_BGRA, // NDIlib.FourCC_type_e fourCC
            (float)_ndiConfig.ResolutionWidth / _ndiConfig.ResolutionHeight, // float aspectRatio
            _ndiConfig.FrameRateN, // int frameRateNumerator
            _ndiConfig.FrameRateD, // int frameRateDenominator
            NDIlib.frame_format_type_e.frame_format_type_progressive // NDIlib.frame_format_type_e format
        );

        // NDIフレームを送出
        _ndiSender.Send(videoFrame);

        await Task.CompletedTask;
    }

    public async Task SetActiveAsync(NdiChannelType channel, bool active)
    {
        if (!IsInitialized) return;

        if (channel == NdiChannelType.Program)
        {
            IsProgramActive = active;
        }
        else if (channel == NdiChannelType.Preview)
        {
            IsPreviewActive = active;
        }
        await Task.CompletedTask;
    }

    public void Dispose()
    {
        _ndiSender?.Dispose();
    }
}
