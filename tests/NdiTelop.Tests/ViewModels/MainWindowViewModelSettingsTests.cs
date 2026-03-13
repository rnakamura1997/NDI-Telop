using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests.ViewModels;

public class MainWindowViewModelSettingsTests
{
    private static MainWindowViewModel CreateViewModel(ISettingsService settingsService)
    {
        var renderService = new RenderService();
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());
        var ndiService = Substitute.For<INdiService>();

        return new MainWindowViewModel(renderService, presetService, ndiService, settingsService);
    }

    [Fact]
    public async Task LoadAppSettingsAsync_ShouldApplyNdiConfigFromSettingsService()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(new AppSettings
        {
            Ndi = new NdiConfig
            {
                SourceName = "Loaded Source",
                ResolutionWidth = 1280,
                ResolutionHeight = 720,
                FrameRateN = 60000,
                FrameRateD = 1001
            }
        });

        var vm = CreateViewModel(settingsService);

        await vm.LoadAppSettingsCommand.ExecuteAsync(null);

        Assert.Equal("Loaded Source", vm.NdiConfig.SourceName);
        Assert.Equal(1280, vm.NdiConfig.ResolutionWidth);
        Assert.Equal(720, vm.NdiConfig.ResolutionHeight);
        Assert.Equal("App settings loaded.", vm.Status);
        await settingsService.Received(1).LoadAsync();
    }

    [Fact]
    public async Task SaveAppSettingsAsync_ShouldCopyCurrentNdiConfigAndInvokeSave()
    {
        var settings = new AppSettings();
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(settings);
        var vm = CreateViewModel(settingsService);
        vm.NdiConfig = new NdiConfig
        {
            SourceName = "To Save",
            ResolutionWidth = 1920,
            ResolutionHeight = 1080,
            FrameRateN = 30000,
            FrameRateD = 1001
        };

        await vm.SaveAppSettingsCommand.ExecuteAsync(null);

        await settingsService.Received(1).SaveAsync();
        Assert.Equal("To Save", settings.Ndi.SourceName);
        Assert.Equal(1920, settings.Ndi.ResolutionWidth);
        Assert.Equal("App settings saved.", vm.Status);
    }

    [Fact]
    public async Task SetProgramActiveAsync_WhenNdiIsActive_ShouldReflectActiveStatusIndicator()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var ndiService = Substitute.For<INdiService>();
        ndiService.IsInitialized.Returns(true);
        ndiService.IsProgramActive.Returns(true);

        var renderService = new RenderService();
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());
        var vm = new MainWindowViewModel(renderService, presetService, ndiService, settingsService);

        await vm.SetProgramActiveCommand.ExecuteAsync(true);

        Assert.Equal("Active", vm.NdiOutputStatus);
        Assert.Equal("#3CB371", vm.NdiOutputStatusColor);
    }

    [Fact]
    public async Task SetPreviewActiveAsync_WhenServiceThrows_ShouldReflectErrorStatusIndicator()
    {
        var settingsService = Substitute.For<ISettingsService>();
        var ndiService = Substitute.For<INdiService>();
        ndiService.SetActiveAsync(NdiChannelType.Preview, true).Returns(_ => throw new InvalidOperationException("boom"));

        var renderService = new RenderService();
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset>());
        var vm = new MainWindowViewModel(renderService, presetService, ndiService, settingsService);

        await vm.SetPreviewActiveCommand.ExecuteAsync(true);

        Assert.Equal("Error", vm.NdiOutputStatus);
        Assert.Equal("#E74C3C", vm.NdiOutputStatusColor);
    }
}
