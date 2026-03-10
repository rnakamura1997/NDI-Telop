using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using Xunit;

namespace NdiTelop.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task PreviewAsync_ShouldUpdatePreviewState()
    {
        var vm = CreateViewModel();
        vm.SelectedPreset = new Preset { Name = "Preview Target" };

        await vm.PreviewAsync();

        Assert.True(vm.IsPreviewReady);
        Assert.Equal("Preview Target", vm.PreviewPreset?.Name);
        Assert.Contains("Preview ready", vm.Status);
    }


    [Fact]
    public async Task PreviewAsync_WhenRenderFails_ShouldKeepStatusSafe()
    {
        var vm = CreateViewModel();
        vm.SelectedPreset = new Preset
        {
            Name = "Broken",
            TextLines = [new TextLine { Text = "X", Color = "not-a-color" }]
        };

        await vm.PreviewAsync();

        Assert.Contains("Preview failed", vm.Status);
    }

    [Fact]
    public async Task TakeAsync_ShouldMovePreviewToProgram()
    {
        var vm = CreateViewModel();
        vm.SelectedPreset = new Preset { Name = "Take Target" };
        await vm.PreviewAsync();

        await vm.TakeAsync();

        Assert.True(vm.IsOnAir);
        Assert.Equal("Take Target", vm.ProgramPreset?.Name);
        Assert.Contains("On Air", vm.Status);
    }

    [Fact]
    public async Task CutAsync_ShouldClearProgramState()
    {
        var vm = CreateViewModel();
        vm.SelectedPreset = new Preset { Name = "Cut Target" };
        await vm.PreviewAsync();
        await vm.TakeAsync();

        await vm.CutAsync();

        Assert.False(vm.IsOnAir);
        Assert.Null(vm.ProgramPreset);
        Assert.Contains("Cut", vm.Status);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        return new MainWindowViewModel(new RenderService(), new PresetService(), new NdiService());
    }
}
