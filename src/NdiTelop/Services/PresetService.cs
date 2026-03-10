using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class PresetService : IPresetService
{
    private readonly List<Preset> _presets = [];

    public IReadOnlyList<Preset> Presets => _presets;

    public Task LoadPresetsAsync() => Task.CompletedTask;
    public Task SavePresetAsync(Preset preset) => Task.CompletedTask;
    public Task DeletePresetAsync(string id) => Task.CompletedTask;
    public Task ImportFromCsvAsync(string filePath) => Task.CompletedTask;
    public Task ExportToCsvAsync(string filePath) => Task.CompletedTask;
}
