using System.Runtime.InteropServices;
using Serilog;

namespace NdiTelop.Services.Output;

public interface IVirtualCameraTransport
{
    IReadOnlyList<string> GetAvailableDevices();

    Task InitializeAsync(string deviceName, CancellationToken cancellationToken = default);

    Task SendFrameAsync(ReadOnlyMemory<byte> bgra32Frame, CancellationToken cancellationToken = default);

    Task ShutdownAsync(CancellationToken cancellationToken = default);
}

public sealed class VirtualCameraOutputBackend : IOutputBackend
{
    private readonly IVirtualCameraTransport _transport;
    private readonly bool _environmentSupported;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _started;
    private string? _deviceName;

    public VirtualCameraOutputBackend()
        : this(new NativeVirtualCameraTransport(), OperatingSystem.IsWindows())
    {
    }

    public VirtualCameraOutputBackend(IVirtualCameraTransport transport, bool environmentSupported = true)
    {
        _transport = transport;
        _environmentSupported = environmentSupported;
    }

    public string BackendName => "VirtualCamera";

    public async Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_started)
            {
                return;
            }

            if (!_environmentSupported)
            {
                Log.Warning("Virtual camera backend is unavailable on this environment. Falling back to no-op mode.");
                return;
            }

            var devices = _transport.GetAvailableDevices();
            var selectedIndex = context.DeviceIndex.GetValueOrDefault(0);
            if (selectedIndex < 0 || selectedIndex >= devices.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(context.DeviceIndex), $"Virtual camera device index {selectedIndex} is out of range. Available={devices.Count}.");
            }

            var deviceName = devices[selectedIndex];
            await _transport.InitializeAsync(deviceName, cancellationToken);
            _started = true;
            _deviceName = deviceName;
            Log.Information("Virtual camera backend initialized. Device={Device}", _deviceName);
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

            var bgraFrame = ConvertToBgra32(payload.Span);
            await _transport.SendFrameAsync(bgraFrame, cancellationToken);
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
            _deviceName = null;
            Log.Information("Virtual camera backend shutdown completed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public IReadOnlyList<string> GetAvailableDevices() => _environmentSupported ? _transport.GetAvailableDevices() : Array.Empty<string>();

    private static ReadOnlyMemory<byte> ConvertToBgra32(ReadOnlySpan<byte> payload)
    {
        if (payload.Length % 4 == 0)
        {
            return payload.ToArray();
        }

        if (payload.Length % 3 != 0)
        {
            throw new InvalidOperationException($"Unsupported virtual camera frame payload length: {payload.Length}. Expected RGB24 or BGRA32 packed data.");
        }

        var pixels = payload.Length / 3;
        var converted = new byte[pixels * 4];
        var srcIndex = 0;
        var dstIndex = 0;

        while (srcIndex < payload.Length)
        {
            var r = payload[srcIndex++];
            var g = payload[srcIndex++];
            var b = payload[srcIndex++];
            converted[dstIndex++] = b;
            converted[dstIndex++] = g;
            converted[dstIndex++] = r;
            converted[dstIndex++] = byte.MaxValue;
        }

        return converted;
    }
}

internal sealed class NativeVirtualCameraTransport : IVirtualCameraTransport
{
    private const string DefaultDeviceName = "OBS Virtual Camera";
    private bool _initialized;

    public IReadOnlyList<string> GetAvailableDevices()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            var count = VirtualCameraNativeMethods.GetDeviceCount();
            if (count <= 0)
            {
                return [DefaultDeviceName];
            }

            var devices = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                devices.Add(VirtualCameraNativeMethods.GetDeviceName(i));
            }

            return devices;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            Log.Warning(ex, "Virtual camera bridge was not found. Falling back to default OBS device name.");
            return [DefaultDeviceName];
        }
    }

    public Task InitializeAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Virtual camera output is supported on Windows only.");
        }

        var result = VirtualCameraNativeMethods.Initialize(deviceName);
        if (result != 0)
        {
            throw new InvalidOperationException($"Virtual camera initialization failed with code {result}.");
        }

        _initialized = true;
        return Task.CompletedTask;
    }

    public Task SendFrameAsync(ReadOnlyMemory<byte> bgra32Frame, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_initialized)
        {
            throw new InvalidOperationException("Virtual camera transport is not initialized.");
        }

        var frame = bgra32Frame.ToArray();
        var handle = GCHandle.Alloc(frame, GCHandleType.Pinned);
        try
        {
            var result = VirtualCameraNativeMethods.SendFrame(handle.AddrOfPinnedObject(), frame.Length);
            if (result != 0)
            {
                throw new InvalidOperationException($"Virtual camera frame send failed with code {result}.");
            }
        }
        finally
        {
            handle.Free();
        }

        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_initialized)
        {
            VirtualCameraNativeMethods.Shutdown();
            _initialized = false;
        }

        return Task.CompletedTask;
    }
}

internal static class VirtualCameraNativeMethods
{
    private const string VirtualCameraBridgeDll = "NdiTelop.VirtualCameraBridge";

    [DllImport(VirtualCameraBridgeDll, EntryPoint = "vcam_get_device_count", CallingConvention = CallingConvention.Cdecl)]
    private static extern int GetDeviceCountNative();

    [DllImport(VirtualCameraBridgeDll, EntryPoint = "vcam_get_device_name", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern IntPtr GetDeviceNameNative(int deviceIndex);

    [DllImport(VirtualCameraBridgeDll, EntryPoint = "vcam_initialize", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int InitializeNative(string deviceName);

    [DllImport(VirtualCameraBridgeDll, EntryPoint = "vcam_send_frame", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SendFrameNative(IntPtr frameBgra32, int frameLength);

    [DllImport(VirtualCameraBridgeDll, EntryPoint = "vcam_shutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ShutdownNative();

    public static int GetDeviceCount() => GetDeviceCountNative();

    public static string GetDeviceName(int index)
    {
        var pointer = GetDeviceNameNative(index);
        return Marshal.PtrToStringUni(pointer) ?? $"Virtual Camera {index}";
    }

    public static int Initialize(string deviceName) => InitializeNative(deviceName);

    public static int SendFrame(IntPtr frameBgra32, int frameLength) => SendFrameNative(frameBgra32, frameLength);

    public static void Shutdown() => ShutdownNative();
}
