using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Interfaces;
using NdiTelop.Logging;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using NdiTelop.Views;
using Serilog;

namespace NdiTelop;

public static class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        AppLogger.Configure();

        try
        {
            Log.Information("Application startup.");
            Services = ConfigureServices();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly during startup.");
            throw;
        }
        finally
        {
            Log.Information("Application shutdown.");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SettingsWindowViewModel>();
        services.AddSingleton<PresetEditorViewModel>();

        services.AddSingleton<INdiService, NdiService>();
        services.AddSingleton<IRenderService, RenderService>();
        services.AddSingleton<IPresetService, PresetService>(provider =>
            new PresetService(
                Path.Combine(AppContext.BaseDirectory, "data", "presets.json"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPresets", "default_presets.json")
            ));
        services.AddSingleton<ExternalControlCoordinator>();
        services.AddSingleton<ISetlistService, SetlistService>();
        services.AddSingleton<IWebApiService, WebApiService>();
        services.AddSingleton<IOscService, OscService>();
        services.AddSingleton<IOutputService, OutputService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        var serviceProvider = services.BuildServiceProvider();

        InitializeExternalControlServices(serviceProvider);

        return serviceProvider;
    }

    private static void InitializeExternalControlServices(IServiceProvider services)
    {
        var settingsService = services.GetRequiredService<ISettingsService>();
        settingsService.LoadAsync().GetAwaiter().GetResult();

        var presetService = services.GetRequiredService<IPresetService>();
        presetService.LoadPresetsAsync().GetAwaiter().GetResult();

        var hotkeyService = services.GetRequiredService<HotkeyService>();
        hotkeyService.ApplySettings(settingsService.Settings.Hotkeys);

        try
        {
            var oscService = services.GetRequiredService<IOscService>();
            oscService.ReceivePort = settingsService.Settings.OscPort;
            oscService.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OSC initialization failed (port: {Port}).", settingsService.Settings.OscPort);
        }

        try
        {
            var webApiService = services.GetRequiredService<IWebApiService>();
            webApiService.Port = settingsService.Settings.WebApiPort;
            webApiService.StartAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Web API initialization failed (port: {Port}).", settingsService.Settings.WebApiPort);
        }
    }
}
