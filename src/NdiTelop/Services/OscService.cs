using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

        FeedbackHost = Environment.GetEnvironmentVariable("NDI_TELOP_OSC_FEEDBACK_HOST") ?? "127.0.0.1";
        FeedbackPort = ReadFeedbackPortFromEnvironment() ?? ReceivePort;
    }

    public int ReceivePort { get; set; } = 8000;

    public string FeedbackHost { get; set; }

    public int FeedbackPort { get; set; }

    public Task StartAsync()
    {
        if (_listenerTask != null && !_listenerTask.IsCompleted)
        {
            return Task.CompletedTask;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _udpClient = new UdpClient(AddressFamily.InterNetwork);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, ReceivePort));
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

    public async Task SendFeedbackAsync(string address, params object[] args)
    {
        if (!TryBuildOscMessage(address, args, out var payload))
        {
            return;
        }

        var endpoint = await ResolveFeedbackEndpointAsync();
        if (endpoint == null)
        {
            return;
        }

        try
        {
            using var sender = new UdpClient(AddressFamily.InterNetwork);
            await sender.SendAsync(payload, payload.Length, endpoint);
        }
        catch (SocketException ex)
        {
            Log.Warning(ex, "OSC feedback send failed to {Host}:{Port}. Feedback is skipped without retry.", FeedbackHost, FeedbackPort);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Unexpected error while sending OSC feedback. Feedback is skipped without retry.");
        }
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

        return Encoding.UTF8.GetString(payload, 0, terminatorIndex);
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

    private static int? ReadFeedbackPortFromEnvironment()
    {
        if (!int.TryParse(Environment.GetEnvironmentVariable("NDI_TELOP_OSC_FEEDBACK_PORT"), out var value))
        {
            return null;
        }

        return value is > 0 and <= 65535 ? value : null;
    }

    private static bool TryBuildOscMessage(string address, object[] args, out byte[] payload)
    {
        payload = Array.Empty<byte>();

        if (string.IsNullOrWhiteSpace(address) || !address.StartsWith('/'))
        {
            Log.Warning("OSC feedback skipped because address is invalid: {Address}", address);
            return false;
        }

        var normalizedArgs = args ?? Array.Empty<object>();
        var typeTags = new StringBuilder(",");
        var argumentBytes = new List<byte>();

        foreach (var arg in normalizedArgs)
        {
            if (!TryEncodeArgument(arg, typeTags, argumentBytes))
            {
                Log.Warning("OSC feedback skipped because an argument type is not supported: {Type}", arg?.GetType().FullName ?? "null");
                return false;
            }
        }

        var addressBytes = PadOscString(Encoding.UTF8.GetBytes(address));
        var typeTagBytes = PadOscString(Encoding.UTF8.GetBytes(typeTags.ToString()));

        payload = new byte[addressBytes.Length + typeTagBytes.Length + argumentBytes.Count];
        Buffer.BlockCopy(addressBytes, 0, payload, 0, addressBytes.Length);
        Buffer.BlockCopy(typeTagBytes, 0, payload, addressBytes.Length, typeTagBytes.Length);
        Buffer.BlockCopy(argumentBytes.ToArray(), 0, payload, addressBytes.Length + typeTagBytes.Length, argumentBytes.Count);
        return true;
    }

    private static bool TryEncodeArgument(object? arg, StringBuilder typeTags, List<byte> output)
    {
        switch (arg)
        {
            case string text:
                typeTags.Append('s');
                output.AddRange(PadOscString(Encoding.UTF8.GetBytes(text)));
                return true;
            case int intValue:
                typeTags.Append('i');
                WriteInt32(output, intValue);
                return true;
            case long longValue:
                typeTags.Append('h');
                WriteInt64(output, longValue);
                return true;
            case float floatValue:
                typeTags.Append('f');
                WriteSingle(output, floatValue);
                return true;
            case double doubleValue:
                typeTags.Append('d');
                WriteDouble(output, doubleValue);
                return true;
            case bool boolValue:
                typeTags.Append(boolValue ? 'T' : 'F');
                return true;
            case null:
                typeTags.Append('N');
                return true;
            default:
                return false;
        }
    }

    private static byte[] PadOscString(byte[] value)
    {
        var sizeWithTerminator = value.Length + 1;
        var padding = (4 - (sizeWithTerminator % 4)) % 4;
        var buffer = new byte[sizeWithTerminator + padding];
        Buffer.BlockCopy(value, 0, buffer, 0, value.Length);
        return buffer;
    }

    private static void WriteInt32(List<byte> output, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        output.AddRange(buffer.ToArray());
    }

    private static void WriteInt64(List<byte> output, long value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        output.AddRange(buffer.ToArray());
    }

    private static void WriteSingle(List<byte> output, float value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(buffer, value);
        output.AddRange(buffer.ToArray());
    }

    private static void WriteDouble(List<byte> output, double value)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buffer, value);
        output.AddRange(buffer.ToArray());
    }

    private async Task<IPEndPoint?> ResolveFeedbackEndpointAsync()
    {
        if (FeedbackPort is <= 0 or > 65535)
        {
            Log.Warning("OSC feedback skipped because destination port is invalid: {Port}", FeedbackPort);
            return null;
        }

        try
        {
            if (IPAddress.TryParse(FeedbackHost, out var ipAddress))
            {
                return new IPEndPoint(ipAddress, FeedbackPort);
            }

            var addresses = await Dns.GetHostAddressesAsync(FeedbackHost);
            var ipv4Address = addresses.FirstOrDefault(static x => x.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4Address == null)
            {
                Log.Warning("OSC feedback skipped because destination host has no IPv4 address: {Host}", FeedbackHost);
                return null;
            }

            return new IPEndPoint(ipv4Address, FeedbackPort);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "OSC feedback skipped because destination host could not be resolved: {Host}", FeedbackHost);
            return null;
        }
    }
}
