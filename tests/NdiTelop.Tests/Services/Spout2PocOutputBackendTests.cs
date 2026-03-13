using NdiTelop.Services.Output;
using Xunit;

namespace NdiTelop.Tests.Services;

public class Spout2PocOutputBackendTests
{
    [Fact]
    public async Task StartSendStop_OnSupportedEnvironment_ShouldInvokeTransport()
    {
        var transport = new RecordingTransport();
        var backend = new Spout2PocOutputBackend(transport, environmentSupported: true);

        await backend.StartAsync(new OutputStartContext(SenderName: "spout-poc"));
        await backend.SendAsync(new byte[] { 1, 2, 3 });
        await backend.StopAsync();

        Assert.Equal(1, transport.InitializeCalls);
        Assert.Equal(1, transport.SendCalls);
        Assert.Equal(1, transport.ShutdownCalls);
        Assert.Equal("spout-poc", transport.LastSenderName);
    }

    [Fact]
    public async Task Start_OnUnsupportedEnvironment_ShouldGracefullyDegradeWithoutThrowing()
    {
        var transport = new RecordingTransport();
        var backend = new Spout2PocOutputBackend(transport, environmentSupported: false);

        var startException = await Record.ExceptionAsync(() => backend.StartAsync(new OutputStartContext(SenderName: "spout-poc")));
        var sendException = await Record.ExceptionAsync(() => backend.SendAsync(new byte[] { 9 }));
        var stopException = await Record.ExceptionAsync(() => backend.StopAsync());

        Assert.Null(startException);
        Assert.Null(sendException);
        Assert.Null(stopException);
        Assert.Equal(0, transport.InitializeCalls);
        Assert.Equal(0, transport.SendCalls);
        Assert.Equal(0, transport.ShutdownCalls);
    }

    [Fact]
    public async Task StartFailure_ShouldBeContainedAndKeepBackendSafe()
    {
        var transport = new RecordingTransport { ThrowOnInitialize = true };
        var backend = new Spout2PocOutputBackend(transport, environmentSupported: true);

        var startException = await Record.ExceptionAsync(() => backend.StartAsync(new OutputStartContext(SenderName: "spout-poc")));
        var sendException = await Record.ExceptionAsync(() => backend.SendAsync(new byte[] { 7 }));
        var stopException = await Record.ExceptionAsync(() => backend.StopAsync());

        Assert.Null(startException);
        Assert.Null(sendException);
        Assert.Null(stopException);
        Assert.Equal(1, transport.InitializeCalls);
        Assert.Equal(0, transport.SendCalls);
        Assert.Equal(0, transport.ShutdownCalls);
    }

    private sealed class RecordingTransport : ISpout2PocTransport
    {
        public int InitializeCalls { get; private set; }

        public int SendCalls { get; private set; }

        public int ShutdownCalls { get; private set; }

        public bool ThrowOnInitialize { get; init; }

        public string? LastSenderName { get; private set; }

        public Task InitializeAsync(string senderName, CancellationToken cancellationToken = default)
        {
            InitializeCalls++;
            LastSenderName = senderName;
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("init failed");
            }

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
