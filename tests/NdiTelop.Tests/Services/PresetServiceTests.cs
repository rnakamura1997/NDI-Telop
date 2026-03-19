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
    public async Task SavePresetAsync_ShouldPersistOverlaySize()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var preset = new Preset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = "Overlay Size Preset",
            Overlays =
            [
                new OverlayItem
                {
                    Path = "assets/overlay.png",
                    X = 12,
                    Y = 34,
                    Width = 456,
                    Height = 123,
                    Opacity = 1.0,
                    IsVisible = true
                }
            ]
        };

        await service.SavePresetAsync(preset);

        var reloaded = CreateService();
        await reloaded.LoadPresetsAsync();

        var savedPreset = reloaded.Presets.Single(p => p.Id == preset.Id);
        var overlay = Assert.Single(savedPreset.Overlays);
        Assert.Equal(456, overlay.Width);
        Assert.Equal(123, overlay.Height);
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
    public async Task DuplicatePresetAsync_ShouldCloneAllSettings_AndAppendToEnd()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var source = new Preset
        {
            Id = "source",
            Name = "My Preset",
            AutoClearSeconds = 9,
            TextStyle = new TextStyleSettings { FontFamily = "Arial", FontSize = 54, Color = "#F0F0F0", OutlineThickness = 3, OutlineColor = "#101010", ShadowOffsetX = 4, ShadowOffsetY = 6, ShadowBlur = 8, ShadowColor = "#66000000" },
            TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Right, VerticalAlignment = VerticalTextAlignment.Bottom, OffsetX = 32, OffsetY = -18 },
            Background = new BackgroundStyle { Type = "image", AssetPath = "assets/bg.png", Alpha = 0.75, Color = "#112233" },
            Animation = new AnimationConfig { InType = "fade", OutType = "wipe", SpeedSeconds = 1.2f, Easing = "EaseInOut" },
            TextLines =
            {
                new TextLine { Text = "Line 1", FontFamily = "Arial", FontSize = 56, Color = "#FFAA00" },
                new TextLine { Text = "Line 2", FontFamily = "Meiryo", FontSize = 44, Color = "#00AACC" }
            },
            Overlays =
            [
                new OverlayItem { Path = "assets/ov1.png", X = 10, Y = 20, Width = 300, Height = 120, Opacity = 0.5, IsVisible = true },
                new OverlayItem { Path = "assets/ov2.png", X = 30, Y = 40, Width = 320, Height = 140, Opacity = 0.9, IsVisible = false }
            ]
        };

        await service.SavePresetAsync(source);

        var duplicated = await service.DuplicatePresetAsync(source.Id);

        Assert.NotNull(duplicated);
        Assert.NotEqual(source.Id, duplicated!.Id);
        Assert.Equal("My Preset (Copy)", duplicated.Name);
        Assert.Equal(source.AutoClearSeconds, duplicated.AutoClearSeconds);
        Assert.Equal(source.TextStyle.FontFamily, duplicated.TextStyle.FontFamily);
        Assert.Equal(source.TextStyle.FontSize, duplicated.TextStyle.FontSize);
        Assert.Equal(source.TextStyle.Color, duplicated.TextStyle.Color);
        Assert.Equal(source.TextStyle.OutlineThickness, duplicated.TextStyle.OutlineThickness);
        Assert.Equal(source.TextStyle.OutlineColor, duplicated.TextStyle.OutlineColor);
        Assert.Equal(source.TextStyle.ShadowOffsetX, duplicated.TextStyle.ShadowOffsetX);
        Assert.Equal(source.TextStyle.ShadowOffsetY, duplicated.TextStyle.ShadowOffsetY);
        Assert.Equal(source.TextStyle.ShadowBlur, duplicated.TextStyle.ShadowBlur);
        Assert.Equal(source.TextStyle.ShadowColor, duplicated.TextStyle.ShadowColor);
        Assert.Equal(source.TextLayout.HorizontalAlignment, duplicated.TextLayout.HorizontalAlignment);
        Assert.Equal(source.TextLayout.VerticalAlignment, duplicated.TextLayout.VerticalAlignment);
        Assert.Equal(source.TextLayout.OffsetX, duplicated.TextLayout.OffsetX);
        Assert.Equal(source.TextLayout.OffsetY, duplicated.TextLayout.OffsetY);
        Assert.Equal(source.Background.Type, duplicated.Background.Type);
        Assert.Equal(source.Background.AssetPath, duplicated.Background.AssetPath);
        Assert.Equal(source.Background.Alpha, duplicated.Background.Alpha);
        Assert.Equal(source.Animation.InType, duplicated.Animation.InType);
        Assert.Equal(source.Animation.OutType, duplicated.Animation.OutType);
        Assert.Equal(source.Animation.SpeedSeconds, duplicated.Animation.SpeedSeconds);
        Assert.Equal(source.Animation.Easing, duplicated.Animation.Easing);
        Assert.Equal(source.TextLines.Select(t => t.Text), duplicated.TextLines.Select(t => t.Text));
        Assert.Equal(source.Overlays.Select(o => o.Path), duplicated.Overlays.Select(o => o.Path));
        Assert.Equal(duplicated.Id, service.Presets.Last().Id);
        Assert.True(File.Exists(_testUserPresetPath));
    }

    [Fact]
    public async Task DuplicatePresetAsync_ShouldAddIncrementalCopySuffix_WhenNameAlreadyExists()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var source = new Preset { Id = "source", Name = "Preset A" };
        await service.SavePresetAsync(source);
        await service.DuplicatePresetAsync(source.Id);

        var secondCopy = await service.DuplicatePresetAsync(source.Id);

        Assert.NotNull(secondCopy);
        Assert.Equal("Preset A (Copy) 2", secondCopy!.Name);
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
        var header = "Id,Name,AutoClearSeconds,BackgroundType,BackgroundColor,BackgroundAlpha,AnimationInType,AnimationOutType,AnimationSpeedSeconds,AnimationEasing,TextLinesJson,OverlaysJson,TextHorizontalAlignment,TextVerticalAlignment,TextOffsetX,TextOffsetY";
        var row = $"imported1,Imported Preset,5,solid,#000000,0.25,fade,cut,0.3,Linear,\"{textLinesJson.Replace("\"", "\"\"")}\",\"{overlaysJson}\",Right,Bottom,24,-12";
        await File.WriteAllTextAsync(csvPath, header + Environment.NewLine + row);

        await service.ImportFromCsvAsync(csvPath);

        var imported = service.Presets.FirstOrDefault(x => x.Id == "imported1");
        Assert.NotNull(imported);
        Assert.Equal("Imported Preset", imported!.Name);
        Assert.Equal(5, imported.AutoClearSeconds);
        Assert.Single(imported.TextLines);
        Assert.Equal("Imported", imported.TextLines[0].Text);
        Assert.Equal(HorizontalTextAlignment.Right, imported.TextLayout.HorizontalAlignment);
        Assert.Equal(VerticalTextAlignment.Bottom, imported.TextLayout.VerticalAlignment);
        Assert.Equal(24, imported.TextLayout.OffsetX);
        Assert.Equal(-12, imported.TextLayout.OffsetY);
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
            TextLines = [new TextLine { Text = "RT" }],
            TextLayout = new TextLayoutSettings { HorizontalAlignment = HorizontalTextAlignment.Left, VerticalAlignment = VerticalTextAlignment.Top, OffsetX = 15, OffsetY = 25 }
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
        Assert.Equal(HorizontalTextAlignment.Left, imported.TextLayout.HorizontalAlignment);
        Assert.Equal(VerticalTextAlignment.Top, imported.TextLayout.VerticalAlignment);
        Assert.Equal(15, imported.TextLayout.OffsetX);
        Assert.Equal(25, imported.TextLayout.OffsetY);
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldIgnoreEmptyAndInvalidRows()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var csvPath = Path.Combine(_testDataDir, "invalid.csv");
        var header = "Id,Name,AutoClearSeconds,BackgroundType,BackgroundColor,BackgroundAlpha,AnimationInType,AnimationOutType,AnimationSpeedSeconds,AnimationEasing,TextLinesJson,OverlaysJson,TextHorizontalAlignment,TextVerticalAlignment,TextOffsetX,TextOffsetY";
        var invalidRow = "bad1,Bad Preset,not_number,solid,#000000,0.1,fade,cut,0.3,Linear,\"[]\",\"[]\",Center,Center,0,0";
        var validRow = "good1,Good Preset,3,solid,#000000,0.1,fade,cut,0.3,Linear,\"[]\",\"[]\",Center,Center,0,0";
        var body = string.Join(Environment.NewLine, new[] { "", header, "", invalidRow, validRow, "" });
        await File.WriteAllTextAsync(csvPath, body);

        await service.ImportFromCsvAsync(csvPath);

        Assert.Null(service.Presets.FirstOrDefault(x => x.Id == "bad1"));
        Assert.NotNull(service.Presets.FirstOrDefault(x => x.Id == "good1"));
    }

    [Fact]
    public async Task SavePresetAsync_ShouldPersistTextStyleSettings()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var preset = new Preset
        {
            Id = "style-preset",
            Name = "Styled Preset",
            TextStyle = new TextStyleSettings
            {
                FontFamily = "Arial",
                FontSize = 60,
                Color = "#ABCDEF",
                OutlineThickness = 2.5f,
                OutlineColor = "#102030",
                ShadowOffsetX = 5,
                ShadowOffsetY = 7,
                ShadowBlur = 4,
                ShadowColor = "#80445566"
            },
            TextLayout = new TextLayoutSettings
            {
                HorizontalAlignment = HorizontalTextAlignment.Right,
                VerticalAlignment = VerticalTextAlignment.Top,
                OffsetX = -12,
                OffsetY = 18
            }
        };

        await service.SavePresetAsync(preset);

        var reloaded = CreateService();
        await reloaded.LoadPresetsAsync();
        var saved = reloaded.Presets.Single(p => p.Id == "style-preset");

        Assert.Equal("Arial", saved.TextStyle.FontFamily);
        Assert.Equal(60, saved.TextStyle.FontSize);
        Assert.Equal("#ABCDEF", saved.TextStyle.Color);
        Assert.Equal(2.5f, saved.TextStyle.OutlineThickness);
        Assert.Equal("#102030", saved.TextStyle.OutlineColor);
        Assert.Equal(5, saved.TextStyle.ShadowOffsetX);
        Assert.Equal(7, saved.TextStyle.ShadowOffsetY);
        Assert.Equal(4, saved.TextStyle.ShadowBlur);
        Assert.Equal("#80445566", saved.TextStyle.ShadowColor);
        Assert.Equal(HorizontalTextAlignment.Right, saved.TextLayout.HorizontalAlignment);
        Assert.Equal(VerticalTextAlignment.Top, saved.TextLayout.VerticalAlignment);
        Assert.Equal(-12, saved.TextLayout.OffsetX);
        Assert.Equal(18, saved.TextLayout.OffsetY);
    }

    [Fact]
    public async Task ImportFromCsvAsync_ShouldSupportLegacyCsvWithoutTextLayoutColumns()
    {
        var service = CreateService();
        await service.LoadPresetsAsync();

        var csvPath = Path.Combine(_testDataDir, "legacy.csv");
        var header = "Id,Name,AutoClearSeconds,BackgroundType,BackgroundColor,BackgroundAlpha,AnimationInType,AnimationOutType,AnimationSpeedSeconds,AnimationEasing,TextLinesJson,OverlaysJson";
        var row = "legacy1,Legacy Preset,4,solid,#000000,0.1,fade,cut,0.3,Linear,\"[]\",\"[]\"";
        await File.WriteAllTextAsync(csvPath, header + Environment.NewLine + row);

        await service.ImportFromCsvAsync(csvPath);

        var imported = service.Presets.Single(x => x.Id == "legacy1");
        Assert.Equal(HorizontalTextAlignment.Center, imported.TextLayout.HorizontalAlignment);
        Assert.Equal(VerticalTextAlignment.Center, imported.TextLayout.VerticalAlignment);
        Assert.Equal(0, imported.TextLayout.OffsetX);
        Assert.Equal(0, imported.TextLayout.OffsetY);
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
