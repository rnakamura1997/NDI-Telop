using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests;

public class ViewModels_MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(IReadOnlyList<Preset>? presets = null, IPresetService? presetService = null)
    {
        var renderService = new RenderService();
        presetService ??= Substitute.For<IPresetService>();
        presetService.Presets.Returns(presets ?? new List<Preset>());

        var ndiService = Substitute.For<INdiService>();
        var settingsService = Substitute.For<ISettingsService>();

        return new MainWindowViewModel(renderService, presetService, ndiService, settingsService);
    }

    [Fact]
    public void Constructor_ShouldInitializeWithDefaultPreset()
    {
        var vm = CreateViewModel();
        Assert.NotNull(vm.SelectedPreset);
        Assert.Equal("New Preset", vm.SelectedPreset.Name);
        Assert.Equal("Ready", vm.Status);
    }

    [Fact]
    public void RenderPreview_ShouldUpdateStatus()
    {
        var vm = CreateViewModel();
        vm.RenderPreviewCommand.Execute(null);
        Assert.Contains("Preview rendered", vm.Status);
    }

    [Fact]
    public async Task MovePresetAsync_ShouldDelegateToPresetService()
    {
        var presetService = Substitute.For<IPresetService>();
        var presets = new List<Preset>
        {
            new() { Id = "a", Name = "A" },
            new() { Id = "b", Name = "B" }
        };
        var vm = CreateViewModel(presets, presetService);

        await vm.MovePresetAsync("a", 1);

        await presetService.Received(1).MovePresetAsync("a", 1);
        Assert.Equal("Preset order updated.", vm.Status);
    }
}
