using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.Interfaces;
using Xunit;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;

namespace NdiTelop.Tests.Services;

public class PresetServiceTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly string _testUserPresetPath;
    private readonly string _testDefaultPresetPath;

    public PresetServiceTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), "NdiTelopTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDir);
        _testUserPresetPath = Path.Combine(_testDataDir, "data", "presets.json");
        _testDefaultPresetPath = Path.Combine(_testDataDir, "Assets", "DefaultPresets", "default_presets.json");

        // Create a dummy default_presets.json for testing
        Directory.CreateDirectory(Path.GetDirectoryName(_testDefaultPresetPath)!);
        File.WriteAllText(_testDefaultPresetPath, "[ { \"id\": \"default1\", \"name\": \"Default 1\" }, { \"id\": \"default2\", \"name\": \"Default 2\" } ]");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataDir))
        {
            Directory.Delete(_testDataDir, true);
        }
    }

    private IPresetService CreateService()
    {
        return new PresetService(_testUserPresetPath, _testDefaultPresetPath);
    }

    [Fact]
    public async Task LoadPresetsAsync_ShouldLoadDefaultPresets_WhenUserPresetsNotExist()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        Assert.Equal(2, service.Presets.Count);
        Assert.Contains(service.Presets, p => p.Name == "Default 1");
        Assert.Contains(service.Presets, p => p.Name == "Default 2");
    }

    [Fact]
    public async Task LoadPresetsAsync_ShouldLoadUserPresets_WhenTheyExist()
    {
        // Create dummy user presets
        Directory.CreateDirectory(Path.GetDirectoryName(_testUserPresetPath)!);
        File.WriteAllText(_testUserPresetPath, "[ { \"id\": \"user1\", \"name\": \"User 1\" }, { \"id\": \"user2\", \"name\": \"User 2\" } ]");

        var service = CreateService();
        await service.LoadPresetsAsync();

        Assert.Equal(2, service.Presets.Count);
        Assert.Contains(service.Presets, p => p.Name == "User 1");
        Assert.Contains(service.Presets, p => p.Name == "User 2");
    }

    [Fact]
    public async Task SavePresetAsync_ShouldAddPreset_WhenNew()
    {
        var service = CreateService();
        await service.LoadPresetsAsync(); // Load defaults

        var newPreset = new Preset { Id = Guid.NewGuid().ToString(), Name = "New Preset" };
        await service.SavePresetAsync(newPreset);

        Assert.Equal(3, service.Presets.Count);
        Assert.Contains(service.Presets, p => p.Name == "New Preset");
        Assert.True(File.Exists(_testUserPresetPath));
        var savedContent = await File.ReadAllTextAsync(_testUserPresetPath);
        Assert.Contains("New Preset", savedContent);
    }

    [Fact]
    public async Task SavePresetAsync_ShouldUpdatePreset_WhenExisting()
    {
        var service = CreateService();
        await service.LoadPresetsAsync(); // Load defaults

        var existingPreset = service.Presets.First();
        existingPreset.Name = "Updated Default 1";
        await service.SavePresetAsync(existingPreset);

        Assert.Equal(2, service.Presets.Count);
        Assert.Contains(service.Presets, p => p.Name == "Updated Default 1");
        Assert.True(File.Exists(_testUserPresetPath));
        var savedContent = await File.ReadAllTextAsync(_testUserPresetPath);
        Assert.Contains("Updated Default 1", savedContent);
    }

    [Fact]
    public async Task DeletePresetAsync_ShouldRemovePreset()
    {
        var service = CreateService();
        await service.LoadPresetsAsync(); // Load defaults

        var presetToDelete = service.Presets.First();
        await service.DeletePresetAsync(presetToDelete.Id);

        Assert.Single(service.Presets);
        Assert.DoesNotContain(service.Presets, p => p.Id == presetToDelete.Id);
        Assert.True(File.Exists(_testUserPresetPath));
        var savedContent = await File.ReadAllTextAsync(_testUserPresetPath);
        Assert.DoesNotContain(presetToDelete.Name, savedContent);
    }


    [Fact]
    public async Task MovePresetAsync_ShouldReorderAndPersist()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var firstPresetId = service.Presets[0].Id;
        await service.MovePresetAsync(firstPresetId, 1);

        Assert.Equal(firstPresetId, service.Presets[1].Id);
        Assert.True(File.Exists(_testUserPresetPath));

        var reloaded = CreateService();
        await reloaded.LoadPresetsAsync();

        Assert.Equal(firstPresetId, reloaded.Presets[1].Id);
    }

    [Fact]
    public async Task ExportPresetAsync_ShouldCreateSchemaBasedJsonFile()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var exportPath = Path.Combine(_testDataDir, "single_preset_export.json");
        var targetPreset = service.Presets.First();

        await service.ExportPresetAsync(exportPath, targetPreset.Id);

        Assert.True(File.Exists(exportPath));

        var json = await File.ReadAllTextAsync(exportPath);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("SchemaVersion").GetString());
        Assert.True(root.TryGetProperty("ExportedAtUtc", out _));

        var presets = root.GetProperty("Presets");
        Assert.Equal(1, presets.GetArrayLength());
        Assert.Equal(targetPreset.Id, presets[0].GetProperty("Id").GetString());
    }

    [Fact]
    public async Task ExportPresetsAsync_AndImportPresetsAsync_ShouldRoundTripMultiplePresets()
    {
        var exporter = CreateService();
        await exporter.LoadPresetsAsync();

        var thirdPreset = new Preset { Id = "custom3", Name = "Custom 3" };
        await exporter.SavePresetAsync(thirdPreset);

        var exportPath = Path.Combine(_testDataDir, "multi_presets_export.json");
        var exportIds = exporter.Presets.Select(x => x.Id).ToList();

        await exporter.ExportPresetsAsync(exportPath, exportIds);

        // Use a separate service instance to verify import behavior
        var importer = CreateService();
        await importer.LoadPresetsAsync();

        var importedCount = await importer.ImportPresetsAsync(exportPath);

        Assert.Equal(exportIds.Count, importedCount);
        Assert.Equal(exportIds.Count, importer.Presets.Count);
        Assert.Contains(importer.Presets, p => p.Name == "Custom 3");
    }

    [Fact]
    public async Task ExportToCsvAsync_ShouldGenerateExpectedCsvWithHeader()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var preset = new Preset
        {
            Id = "csv1",
            Name = "CSV Preset",
            AutoClearSeconds = 12,
            Background = new BackgroundStyle { Type = "solid", Color = "#112233", Alpha = 0.5 },
            Animation = new AnimationConfig { InType = "fade", OutType = "slide", SpeedSeconds = 0.7f, Easing = "EaseIn" },
            TextLines = [new TextLine { Text = "Line1", FontFamily = "Meiryo", FontSize = 40, Color = "#FFFFFF" }],
            Overlays = [new OverlayItem { Path = "overlay.png", X = 10, Y = 20, Width = 100, Height = 80, Opacity = 0.8, IsVisible = true }]
        };

        await service.SavePresetAsync(preset);

        var csvPath = Path.Combine(_testDataDir, "export.csv");
        await service.ExportToCsvAsync(csvPath);

        Assert.True(File.Exists(csvPath));
        var lines = await File.ReadAllLinesAsync(csvPath);
        Assert.True(lines.Length >= 2);
        Assert.Contains("Id,Name,AutoClearSeconds", lines[0]);
        Assert.Contains("csv1", string.Join('\n', lines));
        Assert.Contains("CSV Preset", string.Join('\n', lines));
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldCreatePresetsFromCsv()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var csvPath = Path.Combine(_testDataDir, "import.csv");
        var textLinesJson = "[{\"Text\":\"Imported\",\"FontFamily\":\"Meiryo\",\"FontSize\":48,\"Color\":\"#FFFFFF\"}]";
        var overlaysJson = "[]";
        var header = "Id,Name,AutoClearSeconds,BackgroundType,BackgroundColor,BackgroundAlpha,AnimationInType,AnimationOutType,AnimationSpeedSeconds,AnimationEasing,TextLinesJson,OverlaysJson";
        var row = $"imported1,Imported Preset,5,solid,#000000,0.25,fade,cut,0.3,Linear,\"{textLinesJson.Replace("\"", "\"\"")}\",\"{overlaysJson}\"";
        await File.WriteAllTextAsync(csvPath, header + Environment.NewLine + row);

        await service.ImportFromCsvAsync(csvPath);

        var imported = service.Presets.FirstOrDefault(x => x.Id == "imported1");
        Assert.NotNull(imported);
        Assert.Equal("Imported Preset", imported!.Name);
        Assert.Equal(5, imported.AutoClearSeconds);
        Assert.Single(imported.TextLines);
        Assert.Equal("Imported", imported.TextLines[0].Text);
    }

    [Fact]
    public async Task ExportToCsvAsync_AndImportFromCsvAsync_ShouldRoundTrip()
    {
        var exporter = CreateService();
        await exporter.LoadPresetsAsync();

        var preset = new Preset
        {
            Id = "roundtrip1",
            Name = "Round Trip",
            AutoClearSeconds = 9,
            Background = new BackgroundStyle { Type = "solid", Color = "#010203", Alpha = 0.3 },
            Animation = new AnimationConfig { InType = "wipe", OutType = "cut", SpeedSeconds = 0.9f, Easing = "Linear" },
            TextLines = [new TextLine { Text = "RT" }]
        };
        await exporter.SavePresetAsync(preset);

        var csvPath = Path.Combine(_testDataDir, "roundtrip.csv");
        await exporter.ExportToCsvAsync(csvPath);

        var importer = CreateService();
        await importer.LoadPresetsAsync();
        await importer.ImportFromCsvAsync(csvPath);

        var imported = importer.Presets.FirstOrDefault(x => x.Id == "roundtrip1");
        Assert.NotNull(imported);
        Assert.Equal("Round Trip", imported!.Name);
        Assert.Equal(9, imported.AutoClearSeconds);
        Assert.Equal("RT", imported.TextLines[0].Text);
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldIgnoreEmptyAndInvalidRows()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var csvPath = Path.Combine(_testDataDir, "invalid.csv");
        var header = "Id,Name,AutoClearSeconds,BackgroundType,BackgroundColor,BackgroundAlpha,AnimationInType,AnimationOutType,AnimationSpeedSeconds,AnimationEasing,TextLinesJson,OverlaysJson";
        var invalidRow = "bad1,Bad Preset,not_number,solid,#000000,0.1,fade,cut,0.3,Linear,\"[]\",\"[]\"";
        var validRow = "good1,Good Preset,3,solid,#000000,0.1,fade,cut,0.3,Linear,\"[]\",\"[]\"";
        var body = string.Join(Environment.NewLine, new[] { "", header, "", invalidRow, validRow, "" });
        await File.WriteAllTextAsync(csvPath, body);

        await service.ImportFromCsvAsync(csvPath);

        Assert.Null(service.Presets.FirstOrDefault(x => x.Id == "bad1"));
        Assert.NotNull(service.Presets.FirstOrDefault(x => x.Id == "good1"));
    }

    [Fact]
    public async Task ImportFromCsvAsync_WithEmptyFile_ShouldNotChangePresets()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();
        var beforeCount = service.Presets.Count;

        var csvPath = Path.Combine(_testDataDir, "empty.csv");
        await File.WriteAllTextAsync(csvPath, string.Empty);

        await service.ImportFromCsvAsync(csvPath);

        Assert.Equal(beforeCount, service.Presets.Count);
    }
}
