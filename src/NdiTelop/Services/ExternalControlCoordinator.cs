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
    public Func<Task>? ClearProgramHandler { get; set; }
    public Func<string>? GetNdiOutputStatusHandler { get; set; }
    public Func<ExternalBasicSettings>? GetBasicSettingsHandler { get; set; }

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

    public async Task<bool> ClearProgramAsync()
    {
        if (ClearProgramHandler == null)
        {
            return false;
        }

        await ClearProgramHandler.Invoke();
        return true;
    }

    public string GetNdiOutputStatus() => GetNdiOutputStatusHandler?.Invoke() ?? "Inactive";

    public ExternalBasicSettings GetBasicSettings() => GetBasicSettingsHandler?.Invoke() ?? new ExternalBasicSettings();
}

public class ExternalBasicSettings
{
    public string NdiSourceName { get; set; } = "NdiTelop";
    public int ResolutionWidth { get; set; } = 1920;
    public int ResolutionHeight { get; set; } = 1080;
    public int FrameRateN { get; set; } = 30000;
    public int FrameRateD { get; set; } = 1001;
    public int WebApiPort { get; set; } = 5000;
    public int OscPort { get; set; } = 8000;
}
