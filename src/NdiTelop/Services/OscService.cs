using System.Net;
using System.Net.Sockets;
using NdiTelop.Interfaces;

namespace NdiTelop.Services;

public class OscService : IOscService
{
    private readonly ExternalControlCoordinator _coordinator;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    public OscService(ExternalControlCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public int ReceivePort { get; set; } = 8000;

    public Task StartAsync()
    {
        if (_listenerTask != null && !_listenerTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        _udpClient = new UdpClient(ReceivePort);
        _listenerTask = ListenLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts == null)
        {
            return;
        }

        _cts.Cancel();
        _udpClient?.Close();

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask;
            }
            catch (OperationCanceledException)
            {
                // no-op
            }
            catch (ObjectDisposedException)
            {
                // no-op
            }
        }

        _udpClient?.Dispose();
        _udpClient = null;
        _cts.Dispose();
        _cts = null;
        _listenerTask = null;
    }

    public Task SendFeedbackAsync(string address, params object[] args)
    {
        // Phase 6では受信制御のみ実装
        return Task.CompletedTask;
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        if (_udpClient == null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult received;
            try
            {
                received = await _udpClient.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            var address = ExtractOscAddress(received.Buffer);
            if (string.IsNullOrWhiteSpace(address))
            {
                continue;
            }

            if (TryGetPresetId(address, out var presetId))
            {
                await _coordinator.ShowPresetByIdAsync(presetId);
            }
        }
    }

    internal static string? ExtractOscAddress(byte[] payload)
    {
        if (payload.Length == 0 || payload[0] != (byte)'/')
        {
            return null;
        }

        var terminatorIndex = Array.IndexOf(payload, (byte)0);
        if (terminatorIndex <= 0)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(payload, 0, terminatorIndex);
    }

    private static bool TryGetPresetId(string address, out string presetId)
    {
        const string prefix = "/telop/show/";
        presetId = string.Empty;

        if (!address.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || address.Length <= prefix.Length)
        {
            return false;
        }

        presetId = address[prefix.Length..];
        return !string.IsNullOrWhiteSpace(presetId);
    }
}
