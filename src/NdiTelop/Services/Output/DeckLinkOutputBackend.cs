using System.Runtime.InteropServices;
using Serilog;

namespace NdiTelop.Services.Output;

public interface IDeckLinkTransport
{
    IReadOnlyList<string> GetAvailableDevices();

    Task InitializeAsync(int deviceIndex, CancellationToken cancellationToken = default);

    Task SendFrameAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public sealed class DeckLinkOutputBackend : IOutputBackend
{
    private readonly IDeckLinkTransport _transport;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;
    private int? _deviceIndex;

    public DeckLinkOutputBackend()
        : this(new DeckLinkSdkTransport())
    {
    }

    public DeckLinkOutputBackend(IDeckLinkTransport transport)
    {
        _transport = transport;
    }

    public string BackendName => "DeckLink";

    public async Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default)
    {
        if (context.DeviceIndex is null || context.DeviceIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(context.DeviceIndex), "DeckLink device index must be 0 or greater.");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            await _transport.InitializeAsync(context.DeviceIndex.Value, cancellationToken);
            _started = true;
            _deviceIndex = context.DeviceIndex;
            Log.Information("DeckLink backend initialized. DeviceIndex={DeviceIndex}", _deviceIndex);
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

            await _transport.ShutdownAsync(cancellationToken);
            _started = false;
            _deviceIndex = null;
            Log.Information("DeckLink backend shutdown completed.");
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

            await _transport.SendFrameAsync(payload, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<string> GetAvailableDevices() => _transport.GetAvailableDevices();
}

internal sealed class DeckLinkSdkTransport : IDeckLinkTransport
{
    private readonly object _sync = new();
    private bool _initialized;
    private string? _selectedDevice;

    public IReadOnlyList<string> GetAvailableDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return DeckLinkSdk.TryEnumerateDeviceNames();
    }

    public Task InitializeAsync(int deviceIndex, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DeckLink SDK output is supported on Windows only.");
        }

        var devices = DeckLinkSdk.TryEnumerateDeviceNames();
        if (deviceIndex >= devices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceIndex), $"DeckLink device index {deviceIndex} is out of range. Available={devices.Count}.");
        }

        lock (_sync)
        {
            _selectedDevice = devices[deviceIndex];
            _initialized = true;
        }

        Log.Information("DeckLink SDK device selected. Device={Device}", _selectedDevice);
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("DeckLink SDK transport is not initialized.");
            }
        }

        // DeckLink SDKに合わせた色空間変換/フォーマット変換はネイティブ連携層で実施する。
        // 現段階ではSDKデバイス選択後にフレームデータ送出要求を処理できることを担保する。
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            _selectedDevice = null;
            _initialized = false;
        }

        return Task.CompletedTask;
    }
}

internal static class DeckLinkSdk
{
    public static IReadOnlyList<string> TryEnumerateDeviceNames()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            var count = NativeMethods.GetDeckLinkDeviceCount();
            if (count <= 0)
            {
                return [];
            }

            var devices = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var ptr = NativeMethods.GetDeckLinkDeviceName(i);
                if (ptr == IntPtr.Zero)
                {
                    continue;
                }

                var name = Marshal.PtrToStringAnsi(ptr);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    devices.Add(name);
                }
            }

            return devices;
        }
        catch (DllNotFoundException)
        {
            Log.Warning("DeckLink SDK library was not found. Device enumeration is unavailable.");
            return [];
        }
        catch (EntryPointNotFoundException)
        {
            Log.Warning("DeckLink SDK bridge entry points were not found. Device enumeration is unavailable.");
            return [];
        }
    }

    private static class NativeMethods
    {
        [DllImport("DeckLinkNativeBridge", CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetDeckLinkDeviceCount();

        [DllImport("DeckLinkNativeBridge", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetDeckLinkDeviceName(int index);
    }
}
