using NdiTelop.Interfaces;
using NdiTelop.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NdiTelop.Services;

public class PresetService : IPresetService
{
    private readonly ObservableCollection<Preset> _presets = [];
    private readonly PresetStorage _storage = new();
    private readonly string _userPresetPath;
    private readonly string _defaultPresetPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public PresetService(string userPresetPath, string defaultPresetPath)
    {
        _userPresetPath = userPresetPath;
        _defaultPresetPath = defaultPresetPath;
    }

    public IReadOnlyList<Preset> Presets => _presets;

    public async Task LoadPresetsAsync()
    {
        _presets.Clear();
        var userPresets = await _storage.LoadFromFileAsync(_userPresetPath);
        if (userPresets.Count > 0)
        {
            foreach (var p in userPresets) _presets.Add(p);
            return;
        }

        var defaultPresets = await _storage.LoadFromFileAsync(_defaultPresetPath);
        foreach (var p in defaultPresets) _presets.Add(p);
    }

    public async Task SavePresetAsync(Preset preset)
    {
        var idx = _presets.ToList().FindIndex(x => x.Id == preset.Id);
        if (idx >= 0) _presets[idx] = preset;
        else _presets.Add(preset);

        await _storage.SaveToFileAsync(_userPresetPath, _presets.ToList());
    }

    public async Task DeletePresetAsync(string id)
    {
        var presetToRemove = _presets.FirstOrDefault(x => x.Id == id);
        if (presetToRemove != null)
        {
            _presets.Remove(presetToRemove);
            await _storage.SaveToFileAsync(_userPresetPath, _presets.ToList());
        }
    }

    public async Task ExportPresetAsync(string filePath, string presetId)
    {
        var preset = _presets.FirstOrDefault(x => x.Id == presetId);
        if (preset == null)
        {
            throw new InvalidOperationException($"Preset not found. Id={presetId}");
        }

        await ExportPresetsInternalAsync(filePath, [preset]);
    }

    public async Task ExportPresetsAsync(string filePath, IReadOnlyList<string> presetIds)
    {
        var selectedPresets = _presets.Where(p => presetIds.Contains(p.Id)).ToList();
        if (selectedPresets.Count == 0)
        {
            throw new InvalidOperationException("No presets selected for export.");
        }

        await ExportPresetsInternalAsync(filePath, selectedPresets);
    }

    public async Task<int> ImportPresetsAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        var imported = await LoadImportPackageAsync(filePath);
        if (imported.Count == 0)
        {
            return 0;
        }

        foreach (var preset in imported)
        {
            var existingIndex = _presets.ToList().FindIndex(p => p.Id == preset.Id);
            if (existingIndex >= 0)
            {
                _presets[existingIndex] = preset;
            }
            else
            {
                _presets.Add(preset);
            }
        }

        await _storage.SaveToFileAsync(_userPresetPath, _presets.ToList());
        return imported.Count;
    }

    private async Task ExportPresetsInternalAsync(string filePath, IReadOnlyList<Preset> presets)
    {
        var package = new PresetTransferPackage
        {
            SchemaVersion = "1.0",
            ExportedAtUtc = DateTimeOffset.UtcNow,
            Presets = presets.ToList()
        };

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, package, _jsonOptions);
    }

    private async Task<List<Preset>> LoadImportPackageAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);

        var package = await JsonSerializer.DeserializeAsync<PresetTransferPackage>(stream, _jsonOptions);
        if (package?.Presets?.Count > 0)
        {
            return package.Presets;
        }

        stream.Position = 0;
        var singlePreset = await JsonSerializer.DeserializeAsync<Preset>(stream, _jsonOptions);
        if (singlePreset != null)
        {
            return [singlePreset];
        }

        stream.Position = 0;
        var presetList = await JsonSerializer.DeserializeAsync<List<Preset>>(stream, _jsonOptions);
        return presetList ?? [];
    }

    public Task ImportFromCsvAsync(string filePath) => Task.CompletedTask;
    public Task ExportToCsvAsync(string filePath) => Task.CompletedTask;
}
