using NSubstitute;
using NdiTelop.Interfaces;
using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using SkiaSharp;
using System;
using System.IO;
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
        Assert.Single(vm.SelectedPreset.GetAllTextBlocks());
        Assert.NotNull(vm.SelectedTextBlock);
        Assert.Equal("Ready", vm.Status);
    }

    [Fact]
    public void AddAndRemoveTextBlock_ShouldManageSelectionAndCollection()
    {
        var vm = CreateViewModel();

        vm.AddTextBlockCommand.Execute(null);

        Assert.Equal(2, vm.SelectedPreset!.GetAllTextBlocks().Count);
        Assert.Equal("Text Block 2", vm.SelectedTextBlock!.Name);

        var originalBlock = vm.SelectedPreset.GetAllTextBlocks().First();
        vm.RemoveTextBlockCommand.Execute(vm.SelectedTextBlock);

        Assert.Single(vm.SelectedPreset.GetAllTextBlocks());
        Assert.Same(originalBlock, vm.SelectedTextBlock);
    }


    [Fact]
    public void AddOverlayFromAsset_ShouldUseImageSizeAndCenterOnDrop()
    {
        var assetRoot = Path.Combine(Path.GetTempPath(), $"NdiTelopVmAssets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(assetRoot);
        var assetPath = Path.Combine(assetRoot, "overlay.png");

        try
        {
            using (var bitmap = new SKBitmap(120, 60))
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(assetPath))
            {
                data.SaveTo(stream);
            }

            var renderService = new RenderService();
            var presetService = Substitute.For<IPresetService>();
            var ndiService = Substitute.For<INdiService>();
            var settingsService = Substitute.For<ISettingsService>();
            var vm = new MainWindowViewModel(renderService, presetService, ndiService, settingsService, assetService: new AssetService(assetRoot));

            vm.AddOverlayFromAsset("overlay.png", 300, 200, centerOnDrop: true);

            var overlay = Assert.Single(vm.SelectedPreset!.GetAllOverlays());
            Assert.Equal(120, overlay.Width);
            Assert.Equal(60, overlay.Height);
            Assert.Equal(240, overlay.X);
            Assert.Equal(170, overlay.Y);
        }
        finally
        {
            if (Directory.Exists(assetRoot))
            {
                Directory.Delete(assetRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void SetSelectedAssetAsOverlay_ShouldAddOverlayFromSelectedAsset()
    {
        var assetRoot = Path.Combine(Path.GetTempPath(), $"NdiTelopVmAssets_{Guid.NewGuid():N}");
        Directory.CreateDirectory(assetRoot);
        var assetPath = Path.Combine(assetRoot, "overlay.png");

        try
        {
            using (var bitmap = new SKBitmap(40, 30))
            using (var image = SKImage.FromBitmap(bitmap))
            using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (var stream = File.OpenWrite(assetPath))
            {
                data.SaveTo(stream);
            }

            var renderService = new RenderService();
            var presetService = Substitute.For<IPresetService>();
            var ndiService = Substitute.For<INdiService>();
            var settingsService = Substitute.For<ISettingsService>();
            var vm = new MainWindowViewModel(renderService, presetService, ndiService, settingsService, assetService: new AssetService(assetRoot));
            vm.SelectedAsset = new AssetItem { RelativePath = "overlay.png", FullPath = assetPath, ThumbnailPath = assetPath, Kind = "Image" };

            vm.SetSelectedAssetAsOverlayCommand.Execute(null);

            var overlay = Assert.Single(vm.SelectedPreset!.GetAllOverlays());
            Assert.Equal("overlay.png", overlay.Path);
            Assert.Equal(40, overlay.Width);
            Assert.Equal(30, overlay.Height);
        }
        finally
        {
            if (Directory.Exists(assetRoot))
            {
                Directory.Delete(assetRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void RenderPreview_ShouldUpdateStatus()
    {
        var vm = CreateViewModel();
        vm.RenderPreviewCommand.Execute(null);
        Assert.Contains("Preview rendered", vm.Status);
    }

    [Fact]
    public void PresetSearch_ShouldFilterPresetsByNameAndClearSelectionWhenNeeded()
    {
        var alpha = new Preset { Id = "p1", Name = "Alpha" };
        var beta = new Preset { Id = "p2", Name = "Beta" };
        var alphabet = new Preset { Id = "p3", Name = "Alphabet" };
        var vm = CreateViewModel(new List<Preset> { alpha, beta, alphabet });
        vm.SelectedPreset = beta;

        vm.PresetSearchKeyword = "alp";

        Assert.Equal(new[] { alpha, alphabet }, vm.FilteredPresets.ToArray());
        Assert.Same(alpha, vm.SelectedPreset);

        vm.ClearPresetSearchCommand.Execute(null);

        Assert.Equal(3, vm.FilteredPresets.Count);
        Assert.Same(alpha, vm.SelectedPreset);
    }

    [Fact]
    public void PresetSearch_ShouldBeCaseInsensitive()
    {
        var preset = new Preset { Id = "p1", Name = "Breaking News" };
        var vm = CreateViewModel(new List<Preset> { preset });

        vm.PresetSearchKeyword = "breaking";

        Assert.Single(vm.FilteredPresets);
        Assert.Same(preset, vm.FilteredPresets[0]);
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
    public async Task DuplicateSelectedPresetAsync_ShouldSelectDuplicatedPreset()
    {
        var source = new Preset { Id = "p1", Name = "Preset1" };
        var duplicate = new Preset { Id = "p2", Name = "Preset1 (Copy)" };

        var presetService = Substitute.For<IPresetService>();
        presetService.Presets.Returns(new List<Preset> { source, duplicate });
        presetService.DuplicatePresetAsync("p1").Returns(duplicate);

        var vm = CreateViewModel(new List<Preset> { source, duplicate }, presetService);
        vm.SelectedPreset = source;

        await vm.DuplicateSelectedPresetCommand.ExecuteAsync(null);

        await presetService.Received(1).DuplicatePresetAsync("p1");
        Assert.Same(duplicate, vm.SelectedPreset);
        Assert.Equal("Preset duplicated: Preset1 (Copy)", vm.Status);
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

    [Fact]
    public async Task RunKeyerAuto_ShouldToggleProgramKeyerAndMarkTransitioning()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });
        vm.IsProgramActive = true;

        await vm.ShowPresetCommand.ExecuteAsync(preset);
        var keyer = preset.GetKeyer(KeyerDestination.Usk1);
        keyer.Animation.InType = "fade";
        keyer.Animation.SpeedSeconds = 0.5f;
        var originalState = keyer.KeyOn;

        await vm.RunKeyerAutoCommand.ExecuteAsync(keyer);

        Assert.Equal(!originalState, keyer.KeyOn);
        Assert.True(keyer.IsTransitioning);
        Assert.Equal($"AUTO: {keyer.Name} {(!originalState ? "ON" : "OFF")}", vm.Status);
    }

    [Fact]
    public async Task RunKeyerAuto_ShouldBeSafe_WhenProgramIsUnavailable()
    {
        var preset = new Preset { Id = "p1", Name = "Preset1" };
        var vm = CreateViewModel(new List<Preset> { preset });
        var keyer = preset.GetKeyer(KeyerDestination.Usk1);

        await vm.RunKeyerAutoCommand.ExecuteAsync(keyer);
        Assert.Contains("Program is empty", vm.Status);

        vm.CurrentProgramPreset = preset;
        await vm.RunKeyerAutoCommand.ExecuteAsync(keyer);
        Assert.Contains("Program channel is inactive", vm.Status);
    }

}
