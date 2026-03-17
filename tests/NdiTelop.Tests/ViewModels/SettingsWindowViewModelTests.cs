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

}
