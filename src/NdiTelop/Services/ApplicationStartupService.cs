using NdiTelop.Interfaces;
using Serilog;

namespace NdiTelop.Services;

public sealed class ApplicationStartupService
{
    private readonly ISettingsService _settingsService;
    private readonly IPresetService _presetService;
    private readonly HotkeyService _hotkeyService;
    private readonly IOscService _oscService;
    private readonly IWebApiService _webApiService;
    private readonly IOutputService _outputService;
    private int _initialized;

    public ApplicationStartupService(
        ISettingsService settingsService,
        IPresetService presetService,
        HotkeyService hotkeyService,
        IOscService oscService,
        IWebApiService webApiService,
        IOutputService outputService)
    {
        _settingsService = settingsService;
        _presetService = presetService;
        _hotkeyService = hotkeyService;
        _oscService = oscService;
        _webApiService = webApiService;
        _outputService = outputService;
    }

    public async Task InitializeAsync()
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0)
        {
            return;
        }

        await _settingsService.LoadAsync();
        await _presetService.LoadPresetsAsync();
        _hotkeyService.ApplySettings(_settingsService.Settings.Hotkeys);

        try
        {
            _oscService.ReceivePort = _settingsService.Settings.RemoteControl.OscPort;
            if (_oscService is OscService concreteOsc)
            {
                concreteOsc.FeedbackHost = _settingsService.Settings.RemoteControl.OscFeedbackHost;
                concreteOsc.FeedbackPort = _settingsService.Settings.RemoteControl.OscFeedbackPort;
            }

            await _oscService.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSC initialization failed (port: {Port}).", _settingsService.Settings.RemoteControl.OscPort);
        }

        try
        {
            if (_webApiService is WebApiService concreteWebApi)
            {
                concreteWebApi.Host = _settingsService.Settings.RemoteControl.WebApiHost;
            }

            _webApiService.Port = _settingsService.Settings.RemoteControl.WebApiPort;
            await _webApiService.StartAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web API initialization failed (port: {Port}).", _settingsService.Settings.RemoteControl.WebApiPort);
        }

        try
        {
            await _outputService.ApplySettingsAsync(_settingsService.Settings.Output);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Output backend initialization failed.");
        }
    }
}
