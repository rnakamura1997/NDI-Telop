using NdiTelop.Interfaces;
using NdiTelop.Models;
using Serilog;

namespace NdiTelop.Services;

public class ExternalControlCoordinator
{
    private readonly IPresetService _presetService;
    private readonly Dictionary<string, bool> _programTallyStates = [];

    public ExternalControlCoordinator(IPresetService presetService)
    {
        _presetService = presetService;
    }

    public Func<Preset, Task>? ShowPresetHandler { get; set; }
    public Func<Preset, Task>? TakePresetHandler { get; set; }
    public Func<Task>? ClearProgramHandler { get; set; }
    public Func<KeyerDestination, bool?, double?, Task<bool>>? SetKeyerStateHandler { get; set; }
    public Func<KeyerDestination, Task<bool>>? RunKeyerAutoHandler { get; set; }
    public Func<string>? GetNdiOutputStatusHandler { get; set; }
    public Func<ExternalBasicSettings>? GetBasicSettingsHandler { get; set; }
    public Func<RemoteControlSettings>? GetRemoteControlSettingsHandler { get; set; }

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

    public async Task<bool> TakePresetByIdAsync(string presetId)
    {
        var preset = _presetService.Presets.FirstOrDefault(x => x.Id == presetId);
        if (preset == null)
        {
            return false;
        }

        if (TakePresetHandler != null)
        {
            await TakePresetHandler.Invoke(preset);
            return true;
        }

        if (ShowPresetHandler != null)
        {
            await ShowPresetHandler.Invoke(preset);
            return true;
        }

        return false;
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

    public RemoteControlSettings GetRemoteControlSettings() => GetRemoteControlSettingsHandler?.Invoke() ?? new RemoteControlSettings();

    public async Task<bool> SetKeyerStateAsync(KeyerDestination destination, bool? keyOn, double? opacity = null)
    {
        if (SetKeyerStateHandler == null)
        {
            return false;
        }

        return await SetKeyerStateHandler.Invoke(destination, keyOn, opacity);
    }

    public async Task<bool> RunKeyerAutoAsync(KeyerDestination destination)
    {
        if (RunKeyerAutoHandler == null)
        {
            return false;
        }

        return await RunKeyerAutoHandler.Invoke(destination);
    }

    public async Task<bool> ApplyTallySignalAsync(TallySignal signal)
    {
        var settings = GetRemoteControlSettings();
        if (!settings.EnableTallyAutoTake)
        {
            return false;
        }

        if (!IsTallySourceAccepted(settings, signal))
        {
            return false;
        }

        var sourceKey = string.IsNullOrWhiteSpace(signal.Source)
            ? signal.RemoteIpAddress
            : signal.Source.Trim();

        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            sourceKey = "unknown";
        }

        var wasProgram = _programTallyStates.TryGetValue(sourceKey, out var current) && current;
        _programTallyStates[sourceKey] = signal.Program;

        if (!signal.Program || wasProgram)
        {
            return false;
        }

        Log.Information("Tally rising edge detected. Source={Source}, Transport={Transport}, Keyer={Keyer}", sourceKey, signal.Transport, settings.TallyAutoTakeKeyer);
        return await RunKeyerAutoAsync(settings.TallyAutoTakeKeyer);
    }

    private static bool IsTallySourceAccepted(RemoteControlSettings settings, TallySignal signal)
    {
        var ipFilter = settings.TallyPartnerIpAddress?.Trim();
        if (!string.IsNullOrWhiteSpace(ipFilter) &&
            !string.Equals(ipFilter, signal.RemoteIpAddress, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var nameFilter = settings.TallyPartnerName?.Trim();
        if (!string.IsNullOrWhiteSpace(nameFilter) &&
            !string.Equals(nameFilter, signal.Source, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!settings.AcceptNdiMetadataTally &&
            string.Equals(signal.Transport, "ndi-metadata", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
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
    public string WebApiHost { get; set; } = "*";
    public string OscFeedbackHost { get; set; } = "127.0.0.1";
    public int OscFeedbackPort { get; set; } = 8000;
    public bool EnableTallyAutoTake { get; set; }
    public string TallyPartnerIpAddress { get; set; } = string.Empty;
    public string TallyPartnerName { get; set; } = string.Empty;
    public string TallyAutoTakeKeyer { get; set; } = KeyerDestination.Usk1.ToDisplayName();
}
