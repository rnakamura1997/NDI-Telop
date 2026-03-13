using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using System.Linq;
using Xunit;

namespace NdiTelop.Tests;

public class ViewModels_MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        IReadOnlyList<Preset>? presets = null,
        IPresetService? presetService = null,
        INdiService? ndiService = null)
    {
        var renderService = new RenderService();
        presetService ??= Substitute.For<IPresetService>();
        presetService.Presets.Returns(presets ?? new List<Preset>());

        ndiService ??= Substitute.For<INdiService>();
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

    [Fact]
    public async Task Take_ShouldApplyPreviewPresetToProgram_WhenProgramIsActive()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });
        vm.IsProgramActive = true;

        await vm.SelectPreviewPresetCommand.ExecuteAsync(preset);
        await vm.TakeCommand.ExecuteAsync(null);

        Assert.Same(preset, vm.CurrentPreviewPreset);
        Assert.Same(preset, vm.CurrentProgramPreset);
        Assert.Equal("TAKE: Preset1", vm.Status);
    }

    [Fact]
    public async Task Cut_ShouldApplyImmediatelyAndIgnoreSamePresetReapply()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });
        vm.IsProgramActive = true;

        await vm.SelectPreviewPresetCommand.ExecuteAsync(preset);
        await vm.CutCommand.ExecuteAsync(null);
        Assert.Equal("CUT: Preset1", vm.Status);

        await vm.CutCommand.ExecuteAsync(null);
        Assert.Contains("already on Program", vm.Status);
    }


    [Fact]
    public async Task TriggerPresetByNumberAsync_ShouldShowMappedPreset()
    {
        var presets = Enumerable.Range(1, 3)
            .Select(i => new Preset { Id = $"p{i}", Name = $"Preset{i}" })
            .ToList();
        var vm = CreateViewModel(presets);

        await vm.TriggerPresetByNumberAsync(2);

        Assert.Same(presets[1], vm.CurrentProgramPreset);
        Assert.Same(presets[1], vm.CurrentPreviewPreset);
        Assert.Equal("NumPad2: Preset2", vm.Status);
    }

    [Fact]
    public async Task TriggerPresetByNumberAsync_ShouldBeSafe_ForInvalidOrUnassignedInput()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });

        await vm.TriggerPresetByNumberAsync(0);
        Assert.Equal("NumPad0 ignored: unsupported key.", vm.Status);

        await vm.TriggerPresetByNumberAsync(9);
        Assert.Equal("NumPad9 ignored: no preset assigned.", vm.Status);
        Assert.Null(vm.CurrentProgramPreset);
    }

    [Fact]
    public async Task AutoClear_ShouldStartCancelAndExpireAsExpected()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1", AutoClearSeconds = 2 };
        var ndiService = Substitute.For<INdiService>();
        ndiService.IsProgramActive.Returns(true);
        ndiService.IsInitialized.Returns(true);

        var vm = CreateViewModel(new List<Preset> { preset }, ndiService: ndiService);

        await vm.ShowPresetCommand.ExecuteAsync(preset);
        Assert.Equal(2, vm.AutoClearRemainingSeconds);

        await vm.HandleAutoClearTickAsync();
        Assert.Equal(1, vm.AutoClearRemainingSeconds);

        await vm.ShowPresetCommand.ExecuteAsync(preset);
        Assert.Equal(0, vm.AutoClearRemainingSeconds);
        Assert.Contains("already on Program", vm.Status);

        var second = new Preset { Id = "p2", Name = "Preset2", AutoClearSeconds = 1 };
        var vm2 = CreateViewModel(new List<Preset> { second }, ndiService: ndiService);
        await vm2.ShowPresetCommand.ExecuteAsync(second);
        await vm2.HandleAutoClearTickAsync();

        Assert.Null(vm2.CurrentProgramPreset);
        Assert.Equal(0, vm2.AutoClearRemainingSeconds);
        Assert.Equal("Program cleared.", vm2.Status);
    }
    [Fact]
    public async Task TakeAndCut_ShouldBeSafe_WhenPreviewIsNotSetOrProgramInactive()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });

        await vm.TakeCommand.ExecuteAsync(null);
        Assert.Equal("TAKE ignored: preview preset is not set.", vm.Status);
        Assert.Null(vm.CurrentProgramPreset);

        await vm.SelectPreviewPresetCommand.ExecuteAsync(preset);
        await vm.CutCommand.ExecuteAsync(null);
        Assert.Equal("CUT ignored: Program channel is inactive.", vm.Status);
        Assert.Null(vm.CurrentProgramPreset);
    }
}
