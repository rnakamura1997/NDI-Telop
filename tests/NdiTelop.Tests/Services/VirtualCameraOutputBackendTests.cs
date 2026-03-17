using NdiTelop.Services.Output;
using Xunit;

namespace NdiTelop.Tests.Services;

public class VirtualCameraOutputBackendTests
{
    [Fact]
    public async Task StartSendStop_OnSupportedEnvironment_ShouldInvokeTransport()
    {
        var transport = new RecordingTransport();
        var backend = new VirtualCameraOutputBackend(transport, environmentSupported: true);

        await backend.StartAsync(new OutputStartContext(DeviceIndex: 1));
        await backend.SendAsync(new byte[] { 1, 2, 3, 4 });
        await backend.StopAsync();

        Assert.Equal(1, transport.InitializeCalls);
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.ShutdownCalls);
        Assert.Equal("Virtual Camera 1", transport.LastDeviceName);
    }

    [Fact]
    public async Task SendRgb24Payload_ShouldConvertToBgra32BeforeTransportSend()
    {
        var transport = new RecordingTransport();
        var backend = new VirtualCameraOutputBackend(transport, environmentSupported: true);

        await backend.StartAsync(new OutputStartContext(DeviceIndex: 0));
        await backend.SendAsync(new byte[] { 10, 20, 30 }); // RGB

        Assert.Equal([30, 20, 10, 255], transport.LastPayload);
    }

    [Fact]
    public async Task Start_OnUnsupportedEnvironment_ShouldGracefullyDegradeWithoutThrowing()
    {
        var transport = new RecordingTransport();
        var backend = new VirtualCameraOutputBackend(transport, environmentSupported: false);

        var startException = await Record.ExceptionAsync(() => backend.StartAsync(new OutputStartContext()));
        var sendException = await Record.ExceptionAsync(() => backend.SendAsync(new byte[] { 9, 9, 9, 9 }));
        var stopException = await Record.ExceptionAsync(() => backend.StopAsync());

        Assert.Null(startException);
        Assert.Null(sendException);
        Assert.Null(stopException);
        Assert.Equal(0, transport.InitializeCalls);
        Assert.Equal(0, transport.SendCalls);
        Assert.Equal(0, transport.ShutdownCalls);
    }

    private sealed class RecordingTransport : IVirtualCameraTransport
    {
        public int InitializeCalls { get; private set; }

        public int SendCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public string? LastDeviceName { get; private set; }

        public byte[] LastPayload { get; private set; } = [];

        public IReadOnlyList<string> GetAvailableDevices() => ["Virtual Camera 0", "Virtual Camera 1"];

        public Task InitializeAsync(string deviceName, CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            LastDeviceName = deviceName;
            return Task.CompletedTask;
        }

        public Task SendFrameAsync(ReadOnlyMemory<byte> bgra32Frame, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            LastPayload = bgra32Frame.ToArray();
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalls++;
            return Task.CompletedTask;
        }
    }
}
