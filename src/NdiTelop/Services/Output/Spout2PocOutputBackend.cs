using Serilog;

namespace NdiTelop.Services.Output;

public interface ISpout2PocTransport
{
    Task InitializeAsync(string senderName, CancellationToken cancellationToken = default);

    Task SendFrameAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public sealed class Spout2PocOutputBackend : IOutputBackend
{
    private readonly ISpout2PocTransport _transport;
    private readonly bool _environmentSupported;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;
    private string? _senderName;

    public Spout2PocOutputBackend()
        : this(new InMemorySpout2PocTransport(), OperatingSystem.IsWindows())
    {
    }

    public Spout2PocOutputBackend(ISpout2PocTransport transport, bool environmentSupported)
    {
        _transport = transport;
        _environmentSupported = environmentSupported;
    }

    public string BackendName => "Spout2";

    public async Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default)
    {
        var senderName = string.IsNullOrWhiteSpace(context.SenderName) ? "NdiTelop-Spout2" : context.SenderName;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            if (!_environmentSupported)
            {
                Log.Warning("Spout2 backend is unavailable on this environment. Falling back to no-op mode.");
                return;
            }

            try
            {
                await _transport.InitializeAsync(senderName, cancellationToken);
                _senderName = senderName;
                _started = true;
                Log.Information("Spout2 PoC backend initialized. SenderName={SenderName}", senderName);
            }
            catch (Exception ex)
            {
                _started = false;
                _senderName = null;
                Log.Warning(ex, "Spout2 backend initialization failed. Falling back to no-op mode.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_started)
            {
                return;
            }

            try
            {
                await _transport.ShutdownAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Spout2 backend shutdown failed. Forcing local stop state.");
            }
            finally
            {
                _started = false;
                _senderName = null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_started)
            {
                return;
            }

            try
            {
                await _transport.SendFrameAsync(payload, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Spout2 backend send failed. SenderName={SenderName}", _senderName ?? "unknown");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<string> GetAvailableDevices() => Array.Empty<string>();
}

internal sealed class InMemorySpout2PocTransport : ISpout2PocTransport
{
    private volatile bool _initialized;

    public Task InitializeAsync(string senderName, CancellationToken cancellationToken = default)
    {
        _initialized = true;
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Spout2 PoC transport is not initialized.");
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _initialized = false;
        return Task.CompletedTask;
    }
}
