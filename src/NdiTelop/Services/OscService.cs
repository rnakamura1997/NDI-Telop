using System.Net.Sockets;
using NdiTelop.Interfaces;
using Serilog;

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

        try
        {
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient(ReceivePort);
            _listenerTask = ListenLoopAsync(_cts.Token);
            Log.Information("OSC listener started on UDP port {Port}.", ReceivePort);
            return Task.CompletedTask;
        }
        catch (SocketException ex)
        {
            Log.Error(ex, "OSC listener failed to start. Port may already be in use: {Port}", ReceivePort);
            _cts?.Dispose();
            _cts = null;
            _udpClient?.Dispose();
            _udpClient = null;
            throw;
        }
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
            try { await _listenerTask; }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        _udpClient?.Dispose();
        _udpClient = null;
        _cts.Dispose();
        _cts = null;
        _listenerTask = null;
        Log.Information("OSC listener stopped.");
    }

    public Task SendFeedbackAsync(string address, params object[] args)
    {
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

    internal static bool TryGetPresetId(string address, out string presetId)
    {
        presetId = string.Empty;

        var prefixes = new[] { "/preset/", "/telop/show/" };
        var prefix = prefixes.FirstOrDefault(p => address.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (prefix == null)
        {
            return false;
        }

        var id = address[prefix.Length..].Trim('/');
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        presetId = id;
        return true;
    }
}
