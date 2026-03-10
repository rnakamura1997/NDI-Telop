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
}
