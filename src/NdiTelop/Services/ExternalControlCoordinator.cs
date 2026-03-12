using NdiTelop.Interfaces;
using NdiTelop.Models;

namespace NdiTelop.Services;

public class ExternalControlCoordinator
{
    private readonly IPresetService _presetService;

    public ExternalControlCoordinator(IPresetService presetService)
    {
        _presetService = presetService;
    }

    public Func<Preset, Task>? ShowPresetHandler { get; set; }

    public IReadOnlyList<Preset> GetPresets() => _presetService.Presets;

    public async Task<bool> ShowPresetByIdAsync(string presetId)
    {
        var preset = _presetService.Presets.FirstOrDefault(x => x.Id == presetId);
        if (preset == null || ShowPresetHandler == null)
        {
            return false;
        }

        await ShowPresetHandler.Invoke(preset);
        return true;
    }
}
