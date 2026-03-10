using System.Collections.Concurrent;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using SkiaSharp;

namespace NdiTelop.Services;

/// <summary>
/// NDI 送出サービス（Phase1: 安全なスタブ実装）。
/// 実環境では NewTek SDK バインディングに置き換える。
/// </summary>
public class NdiService : INdiService
{
    private readonly ConcurrentQueue<(NdiChannelType Channel, SKBitmap Frame)> _queue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _workerTask;
    private readonly object _sync = new();
    private NdiConfig? _config;

    public bool IsInitialized { get; private set; }
    public bool IsProgramActive { get; private set; }
    public bool IsPreviewActive { get; private set; }

    public NdiService()
    {
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    public Task InitializeAsync(NdiConfig config)
    {
        lock (_sync)
        {
            _config = config;
            IsInitialized = true;
        }

        return Task.CompletedTask;
    }

    public Task SendFrameAsync(NdiChannelType channel, SKBitmap frame)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("NDI サービスが初期化されていません。");
        }

        var active = channel == NdiChannelType.Program ? IsProgramActive : IsPreviewActive;
        if (!active)
        {
            return Task.CompletedTask;
        }

        _queue.Enqueue((channel, frame.Copy()));
        return Task.CompletedTask;
    }

    public Task SetActiveAsync(NdiChannelType channel, bool active)
    {
        if (channel == NdiChannelType.Program)
        {
            IsProgramActive = active;
        }

        if (channel == NdiChannelType.Preview)
        {
            IsPreviewActive = active;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _workerTask.GetAwaiter().GetResult();
        }
        catch
        {
            Console.Error.WriteLine("NdiService dispose error: worker termination failed.");
        }

        while (_queue.TryDequeue(out var pending))
        {
            pending.Frame.Dispose();
        }

        _cts.Dispose();
    }

    private async Task ProcessQueueAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            while (_queue.TryDequeue(out var item))
            {
                // Phase1 では SDK 未接続のため、キュー処理のみ行ってフレームを破棄する。
                item.Frame.Dispose();
            }

            var sleepMs = 16;
            if (_config is not null && _config.FrameRate > 0)
            {
                sleepMs = (int)Math.Clamp(1000d / _config.FrameRate, 1, 33);
            }

            try
            {
                await Task.Delay(sleepMs, _cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
