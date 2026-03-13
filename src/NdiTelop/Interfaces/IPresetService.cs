using NdiTelop.Models;

namespace NdiTelop.Interfaces;

public interface IPresetService
{
    IReadOnlyList<Preset> Presets { get; }
    Task LoadPresetsAsync();
    Task SavePresetAsync(Preset preset);
    Task DeletePresetAsync(string id);
    Task ImportFromCsvAsync(string filePath);
    Task ExportToCsvAsync(string filePath);
    Task ExportPresetAsync(string filePath, string presetId);
    Task ExportPresetsAsync(string filePath, IReadOnlyList<string> presetIds);
    Task<int> ImportPresetsAsync(string filePath);
}
