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
        catch (PlatformNotSupportedException ex)
        {
            Log.Fatal(ex, "Avalonia desktop lifetime is not supported on the current platform/environment.");
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
            .LogToTrace()
            .UseReactiveUI();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        RegisterServices(services);

        return services.BuildServiceProvider();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<SettingsWindow>();
        services.AddSingleton<HotkeyService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<SettingsWindowViewModel>();
        services.AddSingleton<PresetEditorViewModel>();

        services.AddSingleton<ApplicationStartupService>();
        services.AddSingleton<ExternalDataSourceService>();
        services.AddSingleton<INdiService, NdiService>();
        services.AddSingleton<RenderService>();
        services.AddSingleton<IRenderService>(provider => provider.GetRequiredService<RenderService>());
        services.AddSingleton<PresetService>(provider =>
            new PresetService(
                Path.Combine(AppContext.BaseDirectory, "data", "presets.json"),
                Path.Combine(AppContext.BaseDirectory, "Assets", "DefaultPresets", "default_presets.json")
            ));
        services.AddSingleton<IPresetService>(provider => provider.GetRequiredService<PresetService>());
        services.AddSingleton<ExternalControlCoordinator>();
        services.AddSingleton<ISetlistService, SetlistService>();
        services.AddSingleton<IWebApiService, WebApiService>();
        services.AddSingleton<IOscService, OscService>();
        services.AddSingleton<IOutputService, OutputService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsService>(provider => provider.GetRequiredService<SettingsService>());
        services.AddSingleton<BackupArchiveService>();
        services.AddSingleton<ThemeService>();
    }
}
