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
}
