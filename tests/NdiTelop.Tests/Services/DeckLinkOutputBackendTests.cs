using NdiTelop.Services.Output;
using Xunit;

namespace NdiTelop.Tests.Services;

public class DeckLinkOutputBackendTests
{
    [Fact]
    public async Task StartSendStop_ShouldInvokeTransportInOrder()
    {
        var transport = new RecordingTransport();
        var backend = new DeckLinkOutputBackend(transport);

        await backend.StartAsync(new OutputStartContext(DeviceIndex: 1));
        await backend.SendAsync(new byte[] { 1, 2, 3, 4 });
        await backend.StopAsync();

        Assert.Equal(1, transport.InitializeCalls);
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.ShutdownCalls);
        Assert.Equal(1, transport.LastDeviceIndex);
    }

    [Fact]
    public async Task SendBeforeStart_ShouldBeIgnored()
    {
        var transport = new RecordingTransport();
        var backend = new DeckLinkOutputBackend(transport);

        await backend.SendAsync(new byte[] { 9 });

        Assert.Equal(0, transport.InitializeCalls);
        Assert.Equal(0, transport.SendCalls);
    }

    [Fact]
    public async Task Start_WithInvalidDeviceIndex_ShouldThrow()
    {
        var transport = new RecordingTransport();
        var backend = new DeckLinkOutputBackend(transport);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => backend.StartAsync(new OutputStartContext(DeviceIndex: -1)));
    }

    [Fact]
    public void GetAvailableDevices_ShouldReturnTransportDevices()
    {
        var transport = new RecordingTransport();
        var backend = new DeckLinkOutputBackend(transport);

        var devices = backend.GetAvailableDevices();

        Assert.Equal(["decklink-0", "decklink-1"], devices);
    }

    private sealed class RecordingTransport : IDeckLinkTransport
    {
        public int InitializeCalls { get; private set; }

        public int SendCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public int LastDeviceIndex { get; private set; }

        public IReadOnlyList<string> GetAvailableDevices() => ["decklink-0", "decklink-1"];

        public Task InitializeAsync(int deviceIndex, CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            LastDeviceIndex = deviceIndex;
            return Task.CompletedTask;
        }

        public Task SendFrameAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync(CancellationToken cancellationToken = default)
        {
            ShutdownCalls++;
            return Task.CompletedTask;
        }
    }
}
