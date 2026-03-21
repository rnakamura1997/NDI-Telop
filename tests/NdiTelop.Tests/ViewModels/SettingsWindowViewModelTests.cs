using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests.ViewModels;

public class SettingsWindowViewModelTests
{
    [Fact]
    public async Task SaveAsync_WhenNdiInitialized_ShouldReinitializeNdiAndSetStatus()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var ndiService = Substitute.For<INdiService>();
        ndiService.IsInitialized.Returns(true);

        var vm = new SettingsWindowViewModel(settingsService, hotkeyService: null, ndiService);
        vm.NdiConfig = new NdiConfig
        {
            SourceName = "Updated Source",
            ResolutionWidth = 1280,
            ResolutionHeight = 720,
            FrameRateN = 60000,
            FrameRateD = 1001
        };

        await vm.SaveCommand.ExecuteAsync(null);

        await settingsService.Received(1).SaveAsync();
        await ndiService.Received(1).ReinitializeAsync(Arg.Is<NdiConfig>(c =>
            c.SourceName == "Updated Source" &&
            c.ResolutionWidth == 1280 &&
            c.ResolutionHeight == 720 &&
            c.FrameRateN == 60000 &&
            c.FrameRateD == 1001));
        Assert.Equal("Settings saved.", vm.Status);
    }

    [Fact]
    public async Task SaveAsync_WhenNdiNotInitialized_ShouldNotReinitializeNdi()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var ndiService = Substitute.For<INdiService>();
        ndiService.IsInitialized.Returns(false);

        var vm = new SettingsWindowViewModel(settingsService, hotkeyService: null, ndiService);

        await vm.SaveCommand.ExecuteAsync(null);

        await ndiService.DidNotReceiveWithAnyArgs().ReinitializeAsync(default!);
        Assert.Equal("Settings saved.", vm.Status);
    }
    [Fact]
    public async Task SaveAsync_ShouldApplyOutputSettingsToOutputService()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var outputService = Substitute.For<IOutputService>();

        var vm = new SettingsWindowViewModel(settingsService, hotkeyService: null, ndiService: null, themeService: null, outputService: outputService)
        {
            SelectedOutputBackend = OutputBackendType.Spout2,
            SpoutSenderName = "DemoSender",
            DeckLinkDeviceIndex = 2
        };

        await vm.SaveCommand.ExecuteAsync(null);

        await outputService.Received(1).ApplySettingsAsync(Arg.Is<OutputSettings>(s =>
            s.SelectedBackend == OutputBackendType.Spout2 &&
            s.SpoutSenderName == "DemoSender" &&
            s.DeckLinkDeviceIndex == 2));
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistLogViewerSettings()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var vm = new SettingsWindowViewModel(settingsService)
        {
            ShowDebugLogs = false,
            ShowInformationLogs = true,
            ShowWarningLogs = false,
            ShowErrorLogs = true,
            ShowFatalLogs = false,
            LogKeyword = "error",
            AutoScrollLogs = false
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(settings.LogViewer.ShowDebug);
        Assert.False(settings.LogViewer.ShowWarning);
        Assert.False(settings.LogViewer.ShowFatal);
        Assert.Equal("error", settings.LogViewer.Keyword);
        Assert.False(settings.LogViewer.AutoScroll);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistRemoteControlSettings()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var vm = new SettingsWindowViewModel(settingsService)
        {
            WebApiHost = "127.0.0.1",
            WebApiPort = 5010,
            OscPort = 8010,
            OscFeedbackHost = "192.168.0.50",
            OscFeedbackPort = 8110,
            EnableTallyAutoTake = true,
            TallyPartnerIpAddress = "192.168.0.1",
            TallyPartnerName = "ATEM Mini",
            TallyAutoTakeKeyer = KeyerDestination.Dsk2,
            AcceptNdiMetadataTally = false
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("127.0.0.1", settings.RemoteControl.WebApiHost);
        Assert.Equal(5010, settings.RemoteControl.WebApiPort);
        Assert.Equal(8010, settings.RemoteControl.OscPort);
        Assert.Equal("192.168.0.50", settings.RemoteControl.OscFeedbackHost);
        Assert.Equal(8110, settings.RemoteControl.OscFeedbackPort);
        Assert.True(settings.RemoteControl.EnableTallyAutoTake);
        Assert.Equal("ATEM Mini", settings.RemoteControl.TallyPartnerName);
        Assert.Equal(KeyerDestination.Dsk2, settings.RemoteControl.TallyAutoTakeKeyer);
        Assert.False(settings.RemoteControl.AcceptNdiMetadataTally);
    }



    [Fact]
    public async Task LoadAsync_ShouldPopulateHotkeyBindingsWithFriendlyDescriptions()
    {
        var settings = new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                Preset1 = "Ctrl+Shift+1",
                Preset2 = "Ctrl+Shift+2",
                Preset3 = "Ctrl+Shift+3",
                Preset4 = "Ctrl+Shift+4",
                Preset5 = "Ctrl+Shift+5",
                ClearProgram = "Ctrl+Shift+0"
            }
        };

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var vm = new SettingsWindowViewModel(settingsService);

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.Collection(vm.HotkeyBindings,
            item =>
            {
                Assert.Equal("Ctrl+Shift+1", item.Shortcut);
                Assert.Equal("プリセット1に切り替え", item.ActionName);
                Assert.Equal("設定のみ", item.RegistrationStatus);
            },
            item => Assert.Equal("プリセット2に切り替え", item.ActionName),
            item => Assert.Equal("プリセット3に切り替え", item.ActionName),
            item => Assert.Equal("プリセット4に切り替え", item.ActionName),
            item => Assert.Equal("プリセット5に切り替え", item.ActionName),
            item =>
            {
                Assert.Equal("Ctrl+Shift+0", item.Shortcut);
                Assert.Equal("プログラムをクリア", item.ActionName);
            });
    }

    [Fact]
    public async Task SaveAsync_ShouldRefreshHotkeyBindingsAfterPersistingSettings()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);

        var vm = new SettingsWindowViewModel(settingsService)
        {
            Preset1Hotkey = "Alt+1",
            ClearProgramHotkey = "Alt+0"
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Alt+1", vm.HotkeyBindings[0].Shortcut);
        Assert.Equal("Alt+0", vm.HotkeyBindings[^1].Shortcut);
    }


    [Fact]
    public async Task CreateBackupAsync_ShouldCreateArchiveAndUpdateStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "NdiTelopVmBackupTests", Guid.NewGuid().ToString("N"));
        try
        {
            var settingsPath = Path.Combine(root, "data", "appsettings.json");
            var presetPath = Path.Combine(root, "data", "presets.json");
            var defaultPresetPath = Path.Combine(root, "Assets", "DefaultPresets", "default_presets.json");
            var assetPath = Path.Combine(root, "data", "assets");
            var backupPath = Path.Combine(root, "exports", "backup.zip");

            Directory.CreateDirectory(Path.GetDirectoryName(defaultPresetPath)!);
            await File.WriteAllTextAsync(defaultPresetPath, "[]");
            Directory.CreateDirectory(assetPath);
            await File.WriteAllTextAsync(Path.Combine(assetPath, "image.png"), "asset");

            var settingsService = new SettingsService(settingsPath);
            settingsService.Settings.AssetPath = assetPath;
            var presetService = new PresetService(presetPath, defaultPresetPath);
            await presetService.LoadPresetsAsync();
            await presetService.SavePresetAsync(new Preset { Id = "preset1", Name = "Preset 1" });
            var backupService = new BackupArchiveService(settingsService, presetService);

            var vm = new SettingsWindowViewModel(settingsService, backupArchiveService: backupService);

            await vm.CreateBackupAsync(backupPath);

            Assert.True(File.Exists(backupPath));
            Assert.Equal($"Backup created: {Path.GetFileName(backupPath)}", vm.Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task RestoreBackupAsync_ShouldReloadSettingsAndUpdateStatus()
    {
        var root = Path.Combine(Path.GetTempPath(), "NdiTelopVmRestoreTests", Guid.NewGuid().ToString("N"));
        try
        {
            var defaultPresetPath = Path.Combine(root, "Assets", "DefaultPresets", "default_presets.json");
            Directory.CreateDirectory(Path.GetDirectoryName(defaultPresetPath)!);
            await File.WriteAllTextAsync(defaultPresetPath, "[]");

            var sourceSettings = new SettingsService(Path.Combine(root, "source", "appsettings.json"));
            var sourcePresetService = new PresetService(Path.Combine(root, "source", "presets.json"), defaultPresetPath);
            await sourcePresetService.LoadPresetsAsync();
            await sourcePresetService.SavePresetAsync(new Preset { Id = "preset-source", Name = "Source Preset" });

            var sourceAssetPath = Path.Combine(root, "source-assets");
            Directory.CreateDirectory(sourceAssetPath);
            await File.WriteAllTextAsync(Path.Combine(sourceAssetPath, "clip.mp4"), "video");
            sourceSettings.Settings.AssetPath = sourceAssetPath;
            sourceSettings.Settings.Ndi.SourceName = "Restored from Backup";

            var backupPath = Path.Combine(root, "exports", "backup.zip");
            var backupCreator = new BackupArchiveService(sourceSettings, sourcePresetService);
            await backupCreator.CreateBackupAsync(backupPath);

            var restoreSettings = new SettingsService(Path.Combine(root, "restore", "appsettings.json"));
            var restorePresetService = new PresetService(Path.Combine(root, "restore", "presets.json"), defaultPresetPath);
            await restorePresetService.LoadPresetsAsync();
            var outputService = Substitute.For<IOutputService>();
            var backupRestoreService = new BackupArchiveService(restoreSettings, restorePresetService);

            var vm = new SettingsWindowViewModel(restoreSettings, outputService: outputService, backupArchiveService: backupRestoreService);

            await vm.RestoreBackupAsync(backupPath);

            Assert.Equal("Restored from Backup", restoreSettings.Settings.Ndi.SourceName);
            await outputService.Received(1).ApplySettingsAsync(Arg.Any<OutputSettings>());
            Assert.Equal($"Backup restored: {Path.GetFileName(backupPath)}", vm.Status);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

}
