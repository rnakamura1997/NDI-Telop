using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Interfaces;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using NdiTelop.Views;
using System;
using System.IO;

namespace NdiTelop;

public static class Program
{
    public static IServiceProvider Services { get; private set; } = default!;

    [STAThread]
    public static void Main(string[] args)
    {
        Services = ConfigureServices();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();
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

        var oscService = services.GetRequiredService<IOscService>();
        oscService.ReceivePort = settingsService.Settings.OscPort;
        oscService.StartAsync().GetAwaiter().GetResult();

        var webApiService = services.GetRequiredService<IWebApiService>();
        webApiService.Port = settingsService.Settings.WebApiPort;
        webApiService.StartAsync().GetAwaiter().GetResult();
    }
}
