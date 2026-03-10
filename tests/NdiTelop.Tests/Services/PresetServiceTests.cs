using NdiTelop.Models;
using NdiTelop.Services;
using Xunit;

namespace NdiTelop.Tests.Services;

public class PresetServiceTests
{
    [Fact]
    public async Task SaveAndLoadPreset_ShouldPersistJson()
    {
        var root = Path.Combine(Path.GetTempPath(), "NdiTelopTests", Guid.NewGuid().ToString("N"));
        var presetDir = Path.Combine(root, "presets");
        Directory.CreateDirectory(root);

        var defaults = Path.Combine(root, "default_presets.json");
        await File.WriteAllTextAsync(defaults, "[]");

        var repository = new PresetJsonRepository(new PresetStoragePaths(presetDir, defaults));
        var service = new PresetService(repository);

        var preset = new Preset
        {
            Id = "p1",
            Name = "保存テスト",
            TextLines = [new TextLine { Text = "hello" }]
        };

        await service.SavePresetAsync(preset);
        await service.LoadPresetsAsync();

        Assert.Single(service.Presets);
        Assert.Equal("保存テスト", service.Presets[0].Name);
    }
}
