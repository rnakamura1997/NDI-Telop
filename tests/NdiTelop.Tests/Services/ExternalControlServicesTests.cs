using System.Net.Sockets;
using System.Net;
using System.Net.Http.Json;
using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class ExternalControlServicesTests
{
    [Fact]
    public async Task OscService_ShouldActivatePreset_WhenTelopShowAddressIsReceived()
    {
        var preset = new Preset { Id = "preset-a", Name = "A" };
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset> { preset });

        var coordinator = new ExternalControlCoordinator(presetService);
        var activated = false;
        coordinator.ShowPresetHandler = p =>
        {
            activated = p.Id == "preset-a";
            return Task.CompletedTask;
        };

        var port = GetFreeTcpPort();
        var oscService = new OscService(coordinator)
        {
            ReceivePort = port
        };

        await oscService.StartAsync();
        try
        {
            using var udpClient = new UdpClient();
            var packet = BuildOscAddressPacket("/telop/show/preset-a");
            await udpClient.SendAsync(packet, packet.Length, "127.0.0.1", port);

            await Task.Delay(200);
            Assert.True(activated);
        }
        finally
        {
            await oscService.StopAsync();
        }
    }

    [Fact]
    public async Task WebApiService_ShouldListAndActivatePresets()
    {
        var preset = new Preset { Id = "preset-api", Name = "API Preset" };
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset> { preset });

        var coordinator = new ExternalControlCoordinator(presetService);
        var activatedId = string.Empty;
        coordinator.ShowPresetHandler = p =>
        {
            activatedId = p.Id;
            return Task.CompletedTask;
        };

        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var listResponse = await client.GetAsync("/api/presets");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var list = await listResponse.Content.ReadFromJsonAsync<List<PresetSummary>>();
            Assert.NotNull(list);
            Assert.Contains(list!, x => x.Id == "preset-api" && x.Name == "API Preset");

            var activateResponse = await client.PostAsync("/api/presets/preset-api/activate", null);
            Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
            Assert.Equal("preset-api", activatedId);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }

    private static byte[] BuildOscAddressPacket(string address)
    {
        var addressBytes = System.Text.Encoding.UTF8.GetBytes(address);
        var length = addressBytes.Length + 1;
        var padding = (4 - (length % 4)) % 4;

        var packet = new byte[length + padding + 4];
        Buffer.BlockCopy(addressBytes, 0, packet, 0, addressBytes.Length);
        packet[length + padding] = (byte)',';
        return packet;
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class PresetSummary
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
