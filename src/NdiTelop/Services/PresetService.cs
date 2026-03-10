using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class PresetService : IPresetService
{
    private readonly List<Preset> _presets = [];
    private readonly PresetStorage _storage = new();
    private readonly string _userPresetPath;
    private readonly string _defaultPresetPath;

    public PresetService()
    {
        var baseDir = AppContext.BaseDirectory;
        _userPresetPath = Path.Combine(baseDir, "data", "presets.json");
        _defaultPresetPath = Path.Combine(baseDir, "Assets", "DefaultPresets", "default_presets.json");
    }

    public IReadOnlyList<Preset> Presets => _presets;

    public async Task LoadPresetsAsync()
    {
        _presets.Clear();
        var userPresets = await _storage.LoadFromFileAsync(_userPresetPath);
        if (userPresets.Count > 0)
        {
            _presets.AddRange(userPresets);
            return;
        }

        var defaultPresets = await _storage.LoadFromFileAsync(_defaultPresetPath);
        _presets.AddRange(defaultPresets);
    }

    public async Task SavePresetAsync(Preset preset)
    {
        var idx = _presets.FindIndex(x => x.Id == preset.Id);
        if (idx >= 0) _presets[idx] = preset;
        else _presets.Add(preset);
        await _storage.SaveToFileAsync(_userPresetPath, _presets);
    }

    public async Task DeletePresetAsync(string id)
    {
        _presets.RemoveAll(x => x.Id == id);
        await _storage.SaveToFileAsync(_userPresetPath, _presets);
    }

    public Task ImportFromCsvAsync(string filePath) => Task.CompletedTask;
    public Task ExportToCsvAsync(string filePath) => Task.CompletedTask;
}
