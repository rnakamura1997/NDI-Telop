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

            if (!CanStartDesktopLifetime())
            {
                Log.Warning("Skipping Avalonia desktop startup because no supported desktop session was detected.");
                return;
            }

            Services = ConfigureServices();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (PlatformNotSupportedException ex)
        {
            Log.Fatal(ex, "Avalonia desktop lifetime is not supported on the current platform/environment.");
            throw;
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

    internal static bool CanStartDesktopLifetime()
        => CanStartDesktopLifetime(Environment.GetEnvironmentVariables());

    internal static bool CanStartDesktopLifetime(System.Collections.IDictionary environmentVariables)
    {
        if (OperatingSystem.IsLinux())
        {
            return HasEnvironmentVariable(environmentVariables, "DISPLAY")
                || HasEnvironmentVariable(environmentVariables, "WAYLAND_DISPLAY")
                || IsSupportedLinuxSessionType(environmentVariables);
        }

        return OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();
    }

    private static bool IsSupportedLinuxSessionType(System.Collections.IDictionary environmentVariables)
    {
        if (!TryGetEnvironmentVariable(environmentVariables, "XDG_SESSION_TYPE", out var sessionType))
        {
            return false;
        }

        return sessionType.Equals("x11", StringComparison.OrdinalIgnoreCase)
            || sessionType.Equals("wayland", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEnvironmentVariable(System.Collections.IDictionary environmentVariables, string key)
        => TryGetEnvironmentVariable(environmentVariables, key, out _);

    private static bool TryGetEnvironmentVariable(System.Collections.IDictionary environmentVariables, string key, out string value)
    {
        value = string.Empty;
        if (!environmentVariables.Contains(key))
        {
            return false;
        }

        value = environmentVariables[key]?.ToString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

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
