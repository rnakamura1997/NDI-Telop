using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class OscServiceTests
{
    [Fact]
    public async Task SendFeedbackAsync_ShouldCompleteWithoutException()
    {
        var coordinator = new ExternalControlCoordinator(Substitute.For<IPresetService>());
        var service = new OscService(coordinator)
        {
            FeedbackHost = "127.0.0.1",
            FeedbackPort = GetFreeUdpPort()
        };

        var exception = await Record.ExceptionAsync(() => service.SendFeedbackAsync("/feedback/ping", "ok", 1));

        Assert.Null(exception);
    }

    [Fact]
    public async Task SendFeedbackAsync_ShouldEncodeAddressAndArguments()
    {
        using var receiver = new UdpClient(AddressFamily.InterNetwork);
        receiver.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        var coordinator = new ExternalControlCoordinator(Substitute.For<IPresetService>());
        var service = new OscService(coordinator)
        {
            FeedbackHost = "127.0.0.1",
            FeedbackPort = port
        };

        await service.SendFeedbackAsync("/feedback/state", "READY", 7, 1.5f, true);

        var receiveTask = receiver.ReceiveAsync();
        var completed = await Task.WhenAny(receiveTask, Task.Delay(1000));
        Assert.Same(receiveTask, completed);

        var packet = (await receiveTask).Buffer;
        var cursor = 0;

        Assert.Equal("/feedback/state", ReadOscString(packet, ref cursor));
        Assert.Equal(",sifT", ReadOscString(packet, ref cursor));
        Assert.Equal("READY", ReadOscString(packet, ref cursor));
        Assert.Equal(7, ReadInt32(packet, ref cursor));
        Assert.Equal(1.5f, ReadFloat(packet, ref cursor));
        Assert.Equal(packet.Length, cursor);
    }

    [Fact]
    public async Task SendFeedbackAsync_ShouldSafelySkipInvalidAddressOrUnsupportedArgs()
    {
        using var receiver = new UdpClient(AddressFamily.InterNetwork);
        receiver.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        var port = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;

        var coordinator = new ExternalControlCoordinator(Substitute.For<IPresetService>());
        var service = new OscService(coordinator)
        {
            FeedbackHost = "127.0.0.1",
            FeedbackPort = port
        };

        await service.SendFeedbackAsync("feedback/missing/slash", "x");
        await service.SendFeedbackAsync("/feedback/unsupported", new DateTime(2025, 1, 1));

        var receiveTask = receiver.ReceiveAsync();
        var completed = await Task.WhenAny(receiveTask, Task.Delay(250));

        Assert.NotSame(receiveTask, completed);
    }

    [Fact]
    public async Task ListenLoopAsync_ShouldStillActivatePreset()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<NdiTelop.Models.Preset>
        {
            new() { Id = "preset-osc", Name = "Osc" }
        });

        var coordinator = new ExternalControlCoordinator(presetService);
        var activated = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.ShowPresetHandler = _ =>
        {
            activated.TrySetResult(true);
            return Task.CompletedTask;
        };

        var service = new OscService(coordinator)
        {
            ReceivePort = GetFreeUdpPort()
        };

        await service.StartAsync();
        try
        {
            using var sender = new UdpClient();
            var packet = BuildAddressOnlyPacket("/telop/show/preset-osc");
            await sender.SendAsync(packet, packet.Length, "127.0.0.1", service.ReceivePort);

            var completed = await Task.WhenAny(activated.Task, Task.Delay(1000));
            Assert.Same(activated.Task, completed);
        }
        finally
        {
            await service.StopAsync();
        }
    }

    private static int GetFreeUdpPort()
    {
        using var listener = new UdpClient(AddressFamily.InterNetwork);
        listener.Client.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)listener.Client.LocalEndPoint!).Port;
    }

    private static byte[] BuildAddressOnlyPacket(string address)
    {
        var addressBytes = Encoding.UTF8.GetBytes(address);
        var addressLength = addressBytes.Length + 1;
        var addressPadding = (4 - (addressLength % 4)) % 4;
        var packet = new byte[addressLength + addressPadding + 4];
        Buffer.BlockCopy(addressBytes, 0, packet, 0, addressBytes.Length);
        packet[addressLength + addressPadding] = (byte)',';
        return packet;
    }

    private static string ReadOscString(byte[] payload, ref int cursor)
    {
        var end = Array.IndexOf(payload, (byte)0, cursor);
        Assert.True(end >= cursor);

        var value = Encoding.UTF8.GetString(payload, cursor, end - cursor);
        var totalLength = end - cursor + 1;
        var padding = (4 - (totalLength % 4)) % 4;
        cursor = end + 1 + padding;
        return value;
    }

    private static int ReadInt32(byte[] payload, ref int cursor)
    {
        var value = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(cursor, 4));
        cursor += 4;
        return value;
    }

    private static float ReadFloat(byte[] payload, ref int cursor)
    {
        var value = BinaryPrimitives.ReadSingleBigEndian(payload.AsSpan(cursor, 4));
        cursor += 4;
        return value;
    }
}
