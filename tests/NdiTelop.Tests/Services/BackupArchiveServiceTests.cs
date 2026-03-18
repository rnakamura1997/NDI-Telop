using System.IO.Compression;
using NdiTelop.Models;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class BackupArchiveServiceTests : IDisposable
{
    private readonly string _rootDir;
    private readonly string _settingsPath;
    private readonly string _presetPath;
    private readonly string _defaultPresetPath;
    private readonly string _assetPath;
    private readonly string _backupPath;

    public BackupArchiveServiceTests()
    {
        _rootDir = Path.Combine(Path.GetTempPath(), "NdiTelopBackupTests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_rootDir, "data", "appsettings.json");
        _presetPath = Path.Combine(_rootDir, "data", "presets.json");
        _defaultPresetPath = Path.Combine(_rootDir, "Assets", "DefaultPresets", "default_presets.json");
        _assetPath = Path.Combine(_rootDir, "data", "assets");
        _backupPath = Path.Combine(_rootDir, "exports", "backup.zip");

        Directory.CreateDirectory(Path.GetDirectoryName(_defaultPresetPath)!);
        File.WriteAllText(_defaultPresetPath, "[]");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDir))
        {
            Directory.Delete(_rootDir, true);
        }
    }

    [Fact]
    public async Task CreateBackupAsync_ShouldIncludeSettingsPresetsAndAssets()
    {
        var settingsService = new SettingsService(_settingsPath);
        settingsService.Settings.Ndi.SourceName = "Backup Source";
        settingsService.Settings.AssetPath = _assetPath;

        var presetService = new PresetService(_presetPath, _defaultPresetPath);
        await presetService.LoadPresetsAsync();
        await presetService.SavePresetAsync(new Preset { Id = "preset-a", Name = "Preset A" });

        Directory.CreateDirectory(_assetPath);
        await File.WriteAllTextAsync(Path.Combine(_assetPath, "image.png"), "png-data");

        var service = new BackupArchiveService(settingsService, presetService);
        await service.CreateBackupAsync(_backupPath);

        Assert.True(File.Exists(_backupPath));

        using var archive = ZipFile.OpenRead(_backupPath);
        Assert.NotNull(archive.GetEntry("settings/appsettings.json"));
        Assert.NotNull(archive.GetEntry("presets/presets.json"));
        Assert.NotNull(archive.GetEntry("assets/image.png"));
        Assert.NotNull(archive.GetEntry("manifest.json"));
    }

    [Fact]
    public async Task RestoreBackupAsync_ShouldRestoreApplicationState()
    {
        var sourceAssetPath = Path.Combine(_rootDir, "source-assets");
        var sourceSettings = new SettingsService(Path.Combine(_rootDir, "source", "appsettings.json"));
        sourceSettings.Settings.Ndi.SourceName = "Restored Source";
        sourceSettings.Settings.WebApiPort = 5999;

        var sourcePresets = new PresetService(Path.Combine(_rootDir, "source", "presets.json"), _defaultPresetPath);
        await sourcePresets.LoadPresetsAsync();
        await sourcePresets.SavePresetAsync(new Preset { Id = "preset-restore", Name = "Restored Preset" });

        Directory.CreateDirectory(sourceAssetPath);
        sourceSettings.Settings.AssetPath = sourceAssetPath;
        await File.WriteAllTextAsync(Path.Combine(sourceAssetPath, "clip.mp4"), "video-bytes");

        var createService = new BackupArchiveService(sourceSettings, sourcePresets);
        await createService.CreateBackupAsync(_backupPath);

        await File.WriteAllTextAsync(Path.Combine(sourceAssetPath, "stale.txt"), "remove-me");

        var restoreSettings = new SettingsService(_settingsPath);
        var restorePresets = new PresetService(_presetPath, _defaultPresetPath);
        await restorePresets.LoadPresetsAsync();

        var restoreService = new BackupArchiveService(restoreSettings, restorePresets);
        await restoreService.RestoreBackupAsync(_backupPath);

        Assert.Equal("Restored Source", restoreSettings.Settings.Ndi.SourceName);
        Assert.Equal(sourceAssetPath, restoreSettings.Settings.AssetPath);
        Assert.Contains(restorePresets.Presets, preset => preset.Name == "Restored Preset");
        Assert.True(File.Exists(Path.Combine(sourceAssetPath, "clip.mp4")));
        Assert.False(File.Exists(Path.Combine(sourceAssetPath, "stale.txt")));
    }
}
