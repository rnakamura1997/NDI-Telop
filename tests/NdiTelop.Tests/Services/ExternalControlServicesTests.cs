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
        coordinator.GetNdiOutputStatusHandler = () => "Active";
        coordinator.GetBasicSettingsHandler = () => new ExternalBasicSettings
        {
            NdiSourceName = "NdiTelop-Test",
            ResolutionWidth = 1280,
            ResolutionHeight = 720,
            FrameRateN = 60000,
            FrameRateD = 1001,
            WebApiPort = 5001,
            OscPort = 9001
        };

        var cleared = false;
        coordinator.ClearProgramHandler = () =>
        {
            cleared = true;
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

            var ndiStatusResponse = await client.GetAsync("/api/status/ndi");
            Assert.Equal(HttpStatusCode.OK, ndiStatusResponse.StatusCode);
            var ndiStatus = await ndiStatusResponse.Content.ReadFromJsonAsync<NdiStatusResponse>();
            Assert.Equal("Active", ndiStatus?.Status);

            var settingsResponse = await client.GetAsync("/api/settings/basic");
            Assert.Equal(HttpStatusCode.OK, settingsResponse.StatusCode);
            var settings = await settingsResponse.Content.ReadFromJsonAsync<ExternalBasicSettings>();
            Assert.Equal("NdiTelop-Test", settings?.NdiSourceName);
            Assert.Equal(1280, settings?.ResolutionWidth);

            var clearResponse = await client.PostAsync("/api/program/clear", null);
            Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
            Assert.True(cleared);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }

    [Fact]
    public async Task WebApiService_ShouldServeWebUiEntrypointAndStaticAssets()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());

        var coordinator = new ExternalControlCoordinator(presetService);
        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var indexResponse = await client.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
            Assert.Equal("text/html", indexResponse.Content.Headers.ContentType?.MediaType);

            var cssResponse = await client.GetAsync("/web-ui.css");
            Assert.Equal(HttpStatusCode.OK, cssResponse.StatusCode);

            var jsResponse = await client.GetAsync("/web-ui.js");
            Assert.Equal(HttpStatusCode.OK, jsResponse.StatusCode);
            Assert.Equal("application/javascript", jsResponse.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }

    [Fact]
    public async Task WebApiService_ShouldReturnNotFound_ForUnknownUrl()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());

        var coordinator = new ExternalControlCoordinator(presetService);
        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var response = await client.GetAsync("/unknown-path");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private sealed class NdiStatusResponse
    {
        public string Status { get; set; } = string.Empty;
    }
}
