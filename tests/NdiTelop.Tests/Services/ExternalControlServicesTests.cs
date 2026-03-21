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
            WebApiHost = "127.0.0.1",
            WebApiPort = 5001,
            OscPort = 9001,
            OscFeedbackHost = "127.0.0.1",
            OscFeedbackPort = 9002,
            EnableTallyAutoTake = true,
            TallyPartnerIpAddress = "127.0.0.1",
            TallyAutoTakeKeyer = "USK2"
        };
        coordinator.GetRemoteControlSettingsHandler = () => new RemoteControlSettings
        {
            WebApiHost = "127.0.0.1",
            WebApiPort = 5001,
            OscPort = 9001,
            OscFeedbackHost = "127.0.0.1",
            OscFeedbackPort = 9002,
            EnableTallyAutoTake = true,
            TallyPartnerIpAddress = "127.0.0.1",
            TallyAutoTakeKeyer = KeyerDestination.Usk2
        };

        var cleared = false;
        coordinator.ClearProgramHandler = () =>
        {
            cleared = true;
            return Task.CompletedTask;
        };
        var takenId = string.Empty;
        coordinator.TakePresetHandler = presetToTake =>
        {
            takenId = presetToTake.Id;
            return Task.CompletedTask;
        };
        var keyerAutoDestination = KeyerDestination.Usk1;
        coordinator.RunKeyerAutoHandler = destination =>
        {
            keyerAutoDestination = destination;
            return Task.FromResult(true);
        };
        KeyerDestination? keyerStateDestination = null;
        bool? keyerState = null;
        coordinator.SetKeyerStateHandler = (destination, isOn, _) =>
        {
            keyerStateDestination = destination;
            keyerState = isOn;
            return Task.FromResult(true);
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
            Assert.Equal("127.0.0.1", settings?.WebApiHost);

            var clearResponse = await client.PostAsync("/api/program/clear", null);
            Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
            Assert.True(cleared);

            var nextCueResponse = await client.PostAsync("/api/playlist/next-cue", null);
            Assert.Equal(HttpStatusCode.NotFound, nextCueResponse.StatusCode);

            var takeResponse = await client.PostAsJsonAsync("/take", new TakeRequest { PresetId = "preset-api" });
            Assert.Equal(HttpStatusCode.OK, takeResponse.StatusCode);
            Assert.Equal("preset-api", takenId);

            var keyerOnResponse = await client.PostAsJsonAsync("/api/keyers/usk2/on", new KeyerControlRequest());
            Assert.Equal(HttpStatusCode.OK, keyerOnResponse.StatusCode);
            Assert.Equal(KeyerDestination.Usk2, keyerStateDestination);
            Assert.True(keyerState);

            var tallyResponse = await client.PostAsJsonAsync("/api/tally", new TallySignal
            {
                Source = "ATEM",
                Program = true
            });
            Assert.Equal(HttpStatusCode.OK, tallyResponse.StatusCode);
            Assert.Equal(KeyerDestination.Usk2, keyerAutoDestination);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }


    [Fact]
    public async Task WebApiService_ShouldExposePlaylistStatusAndNextCue()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());

        var coordinator = new ExternalControlCoordinator(presetService);
        var nextCueTriggered = false;
        coordinator.NextCueHandler = () =>
        {
            nextCueTriggered = true;
            return Task.CompletedTask;
        };
        coordinator.GetPlaylistSnapshotHandler = () => new PlaylistStatusSnapshot
        {
            CurrentIndex = 1,
            IsRunning = true,
            AutoAdvanceEnabled = true,
            RemainingSeconds = 4,
            CurrentPresetId = "preset-1",
            CurrentPresetName = "Current",
            NextPresetId = "preset-2",
            NextPresetName = "Next",
            Items = new List<PlaylistStatusItem>
            {
                new() { Index = 0, PresetId = "preset-0", PresetName = "Warmup", DisplayDurationSeconds = 3 },
                new() { Index = 1, PresetId = "preset-1", PresetName = "Current", DisplayDurationSeconds = 4 }
            }
        };

        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Host = "127.0.0.1",
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            var statusResponse = await client.GetFromJsonAsync<PlaylistStatusSnapshot>("/api/playlist/status");
            Assert.NotNull(statusResponse);
            Assert.Equal("Current", statusResponse!.CurrentPresetName);
            Assert.Equal("Next", statusResponse.NextPresetName);
            Assert.Equal(2, statusResponse.Items.Count);

            var nextCueResponse = await client.PostAsync("/api/playlist/next-cue", null);
            Assert.Equal(HttpStatusCode.OK, nextCueResponse.StatusCode);
            Assert.True(nextCueTriggered);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }

    [Fact]
    public async Task WebApiService_ShouldAcceptNdiMetadataTally()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());
        var coordinator = new ExternalControlCoordinator(presetService);
        coordinator.GetRemoteControlSettingsHandler = () => new RemoteControlSettings
        {
            EnableTallyAutoTake = true,
            AcceptNdiMetadataTally = true
        };

        var autoCount = 0;
        coordinator.RunKeyerAutoHandler = _ =>
        {
            autoCount++;
            return Task.FromResult(true);
        };

        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Host = "127.0.0.1",
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            using var content = new StringContent("<tally source=\"camera-1\" program=\"true\" />", System.Text.Encoding.UTF8, "application/xml");
            var response = await client.PostAsync("/api/tally/ndi-metadata", content);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, autoCount);
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
    public async Task WebApiService_ShouldReturnNotFound_WhenCoordinatorHandlerFails()
    {
        var preset = new Preset { Id = "preset-error", Name = "Broken Preset" };
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset> { preset });

        var coordinator = new ExternalControlCoordinator(presetService);
        coordinator.ShowPresetHandler = _ => throw new InvalidOperationException("boom");

        var port = GetFreeTcpPort();
        var webApiService = new WebApiService(coordinator)
        {
            Host = "127.0.0.1",
            Port = port
        };

        await webApiService.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
            var response = await client.PostAsync("/api/presets/preset-error/activate", null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await webApiService.StopAsync();
        }
    }

    [Fact]
    public async Task OscService_StartAsync_ShouldNotThrow_WhenPortIsAlreadyInUse()
    {
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());
        var coordinator = new ExternalControlCoordinator(presetService);
        var port = GetFreeTcpPort();
        using var occupied = new UdpClient(port);

        var oscService = new OscService(coordinator)
        {
            ReceivePort = port
        };

        await oscService.StartAsync();
        await oscService.StopAsync();
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
