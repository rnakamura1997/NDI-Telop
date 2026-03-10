using NdiTelop.Models;
using NdiTelop.Services;
using NdiTelop.Interfaces;
using Xunit;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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
}
