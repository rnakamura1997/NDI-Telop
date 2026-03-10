using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

/// <summary>
/// プリセット管理サービス。
/// </summary>
public class PresetService : IPresetService
{
    private readonly List<Preset> _presets = [];
    private readonly PresetJsonRepository _repository;

    public PresetService()
        : this(CreateDefaultRepository())
    {
    }

    public PresetService(PresetJsonRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Preset> Presets => _presets;

    public async Task LoadPresetsAsync()
    {
        var loaded = await _repository.LoadAllAsync();
        _presets.Clear();
        _presets.AddRange(loaded);
    }

    public async Task SavePresetAsync(Preset preset)
    {
        await _repository.SaveAsync(preset);

        var index = _presets.FindIndex(x => x.Id == preset.Id);
        if (index >= 0)
        {
            _presets[index] = preset;
        }
        else
        {
            _presets.Add(preset);
        }
    }

    public async Task DeletePresetAsync(string id)
    {
        await _repository.DeleteAsync(id);
        _presets.RemoveAll(x => x.Id == id);
    }

    public Task ImportFromCsvAsync(string filePath)
    {
        // Phase4 実装予定
        return Task.CompletedTask;
    }

    public Task ExportToCsvAsync(string filePath)
    {
        // Phase4 実装予定
        return Task.CompletedTask;
    }

    private static PresetJsonRepository CreateDefaultRepository()
    {
        var baseDir = AppContext.BaseDirectory;
        var paths = new PresetStoragePaths(
            Path.Combine(baseDir, "data", "presets"),
            Path.Combine(baseDir, "Assets", "DefaultPresets", "default_presets.json"));

        return new PresetJsonRepository(paths);
    }
}
