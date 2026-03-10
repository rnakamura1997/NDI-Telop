using NdiTelop.Interfaces;
using NdiTelop.Models;
using System.Collections.ObjectModel;

namespace NdiTelop.Services;

public class PresetService : IPresetService
{
    private readonly ObservableCollection<Preset> _presets = [];
    private readonly PresetStorage _storage = new();
    private readonly string _userPresetPath;
    private readonly string _defaultPresetPath;

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

    public Task ImportFromCsvAsync(string filePath) => Task.CompletedTask;
    public Task ExportToCsvAsync(string filePath) => Task.CompletedTask;
}
