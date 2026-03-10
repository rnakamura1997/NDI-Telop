using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

/// <summary>
/// NDI 出力の将来実装に備えた安全な基礎サービス。
/// 現段階ではフレーム保持と状態遷移のみを管理し、実送出には依存しない。
/// </summary>
public sealed class NdiService : INdiService
{
    private readonly object _sync = new();
    private SKBitmap? _programFrame;
    private SKBitmap? _previewFrame;

    public bool IsInitialized { get; private set; }
    public bool IsProgramActive { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public string LastStatusMessage { get; private set; } = "NDI standby";

    public void Dispose()
    {
        lock (_sync)
        {
            _programFrame?.Dispose();
            _previewFrame?.Dispose();
            _programFrame = null;
            _previewFrame = null;
            IsProgramActive = false;
            IsPreviewActive = false;
            IsInitialized = false;
            LastStatusMessage = "NDI disposed";
        }
    }

    public Task InitializeAsync(NdiConfig config)
    {
        lock (_sync)
        {
            IsInitialized = true;
            LastStatusMessage = "NDI initialized (safe mode)";
        }

        return Task.CompletedTask;
    }

    public Task SetActiveAsync(NdiChannelType channel, bool active)
    {
        lock (_sync)
        {
            EnsureInitialized();
            if (channel == NdiChannelType.Program)
            {
                IsProgramActive = active;
                LastStatusMessage = active ? "Program channel active" : "Program channel inactive";
                return Task.CompletedTask;
            }

            IsPreviewActive = active;
            LastStatusMessage = active ? "Preview channel active" : "Preview channel inactive";
            return Task.CompletedTask;
        }
    }

    public Task SendFrameAsync(NdiChannelType channel, SKBitmap frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            EnsureInitialized();
            var copy = frame.Copy();
            if (channel == NdiChannelType.Program)
            {
                _programFrame?.Dispose();
                _programFrame = copy;
                IsProgramActive = true;
                LastStatusMessage = $"Program frame updated ({copy.Width}x{copy.Height})";
                return Task.CompletedTask;
            }

            _previewFrame?.Dispose();
            _previewFrame = copy;
            IsPreviewActive = true;
            LastStatusMessage = $"Preview frame updated ({copy.Width}x{copy.Height})";
            return Task.CompletedTask;
        }
    }

    public Task<bool> TakePreviewToProgramAsync()
    {
        lock (_sync)
        {
            EnsureInitialized();
            if (_previewFrame is null)
            {
                LastStatusMessage = "Take skipped: preview frame is empty";
                return Task.FromResult(false);
            }

            _programFrame?.Dispose();
            _programFrame = _previewFrame.Copy();
            IsProgramActive = true;
            LastStatusMessage = "Take executed: preview -> program";
            return Task.FromResult(true);
        }
    }

    public Task CutProgramAsync()
    {
        lock (_sync)
        {
            EnsureInitialized();
            _programFrame?.Dispose();
            _programFrame = null;
            IsProgramActive = false;
            LastStatusMessage = "Cut executed: program cleared";
            return Task.CompletedTask;
        }
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            IsInitialized = true;
            LastStatusMessage = "NDI auto-initialized (safe mode)";
        }
    }
}
