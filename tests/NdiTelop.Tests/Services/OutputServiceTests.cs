using Xunit;
using NdiTelop.Services;
using NdiTelop.Services.Output;

namespace NdiTelop.Tests.Services;

public class OutputServiceTests
{
    [Fact]
    public async Task StartSendStop_ShouldFollowNormalSequence()
    {
        var backend = new TestOutputBackend("VirtualCamera");
        var service = CreateService(backend, new TestOutputBackend("DeckLink"), new TestOutputBackend("Spout2"));

        await service.StartVirtualCameraAsync();
        await service.SendVirtualCameraFrameAsync(new byte[] { 1, 2, 3 });
        await service.StopVirtualCameraAsync();

        Assert.Equal(1, backend.StartCalls);
        Assert.Equal(1, backend.SendCalls);
        Assert.Equal(1, backend.StopCalls);
    }

    [Fact]
    public async Task SendBeforeStart_DoubleStart_DoubleStop_ShouldBehaveSafely()
    {
        var backend = new TestOutputBackend("VirtualCamera");
        var service = CreateService(backend, new TestOutputBackend("DeckLink"), new TestOutputBackend("Spout2"));

        await service.SendVirtualCameraFrameAsync(new byte[] { 9 });
        await service.StartVirtualCameraAsync();
        await service.StartVirtualCameraAsync();
        await service.StopVirtualCameraAsync();
        await service.StopVirtualCameraAsync();

        Assert.Equal(1, backend.StartCalls);
        Assert.Equal(0, backend.SendCalls);
        Assert.Equal(1, backend.StopCalls);
    }


    [Fact]
    public async Task SpoutBackendSelection_ShouldCallSpoutBackendOnly()
    {
        var virtualCamera = new TestOutputBackend("VirtualCamera");
        var deckLink = new TestOutputBackend("DeckLink");
        var spout = new TestOutputBackend("Spout2");
        var service = CreateService(virtualCamera, deckLink, spout);

        await service.StartSpoutAsync("poc-sender");
        await service.SendSpoutFrameAsync(new byte[] { 4, 5, 6 });
        await service.StopSpoutAsync();

        Assert.Equal(0, virtualCamera.StartCalls);
        Assert.Equal(0, deckLink.StartCalls);
        Assert.Equal(1, spout.StartCalls);
        Assert.Equal(1, spout.SendCalls);
        Assert.Equal(1, spout.StopCalls);
    }

    [Fact]
    public async Task BackendExceptions_ShouldNotEscapeAndShouldRemainStable()
    {
        var failingBackend = new TestOutputBackend("VirtualCamera")
        {
            ThrowOnStart = true,
            ThrowOnStop = true,
            ThrowOnSend = true,
        };

        var service = CreateService(failingBackend, new TestOutputBackend("DeckLink"), new TestOutputBackend("Spout2"));

        var startException = await Record.ExceptionAsync(() => service.StartVirtualCameraAsync());
        var sendException = await Record.ExceptionAsync(() => service.SendVirtualCameraFrameAsync(new byte[] { 1 }));
        var stopException = await Record.ExceptionAsync(() => service.StopVirtualCameraAsync());

        Assert.Null(startException);
        Assert.Null(sendException);
        Assert.Null(stopException);
        Assert.Equal(1, failingBackend.StartCalls);
        Assert.Equal(0, failingBackend.SendCalls);
        Assert.Equal(0, failingBackend.StopCalls);
    }

    private static OutputService CreateService(IOutputBackend virtualCamera, IOutputBackend deckLink, IOutputBackend spout)
        => new(virtualCamera, deckLink, spout);

    private sealed class TestOutputBackend(string backendName) : IOutputBackend
    {
        public string BackendName { get; } = backendName;

        public int StartCalls { get; private set; }

        public int StopCalls { get; private set; }

        public int SendCalls { get; private set; }

        public bool ThrowOnStart { get; init; }

        public bool ThrowOnStop { get; init; }

        public bool ThrowOnSend { get; init; }

        public Task StartAsync(OutputStartContext context, CancellationToken cancellationToken = default)
        {
            StartCalls++;
            if (ThrowOnStart)
            {
                throw new InvalidOperationException("start failed");
            }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalls++;
            if (ThrowOnStop)
            {
                throw new InvalidOperationException("stop failed");
            }

            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            SendCalls++;
            if (ThrowOnSend)
            {
                throw new InvalidOperationException("send failed");
            }

            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetAvailableDevices() => new[] { "mock-device" };
    }
}
