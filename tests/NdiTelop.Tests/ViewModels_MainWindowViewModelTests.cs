using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests;

public class ViewModels_MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(IReadOnlyList<Preset>? presets = null)
    {
        var renderService = new RenderService();
        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(presets ?? new List<Preset>());

        var ndiService = Substitute.For<INdiService>();
        return new MainWindowViewModel(renderService, presetService, ndiService);
    }

    [Fact]
    public void AddOverlay_ShouldAppendOverlayToSelectedPreset()
    {
        var preset = new Preset { Name = "P1" };
        var vm = CreateViewModel([preset]);
        vm.SelectedPreset = preset;

        vm.AddOverlayCommand.Execute(null);

        Assert.Single(vm.OverlayItems);
        Assert.Single(preset.Overlays);
        Assert.NotNull(vm.SelectedOverlay);
        Assert.Equal("Overlay added.", vm.Status);
    }

    [Fact]
    public void RemoveSelectedOverlay_ShouldRemoveFromPresetAndList()
    {
        var preset = new Preset { Name = "P1" };
        preset.Overlays.Add(new OverlayItem { Path = "a.png", X = 10, Y = 20, Width = 100, Height = 80, Opacity = 1 });

        var vm = CreateViewModel([preset]);
        vm.SelectedPreset = preset;
        vm.SelectedOverlay = vm.OverlayItems.First();

        vm.RemoveSelectedOverlayCommand.Execute(null);

        Assert.Empty(vm.OverlayItems);
        Assert.Empty(preset.Overlays);
        Assert.Null(vm.SelectedOverlay);
        Assert.Equal("Overlay removed.", vm.Status);
    }
}
