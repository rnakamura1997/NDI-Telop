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
    private static readonly string[] SupportedWindowsSessionPrefixes = ["Console", "RDP-", "ICA-"];

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
                Log.Error("Skipping Avalonia desktop startup because the current process is not running in a supported interactive Windows desktop session.");
                return;
            }

            Services = ConfigureServices();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (PlatformNotSupportedException ex)
        {
            Log.Error(ex, "Avalonia desktop lifetime is not supported in the current execution environment. Run the application in an interactive Windows desktop session.");
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
        => CanStartDesktopLifetime(
            Environment.GetEnvironmentVariables(),
            Environment.UserInteractive,
            Environment.GetEnvironmentVariable("SESSIONNAME"));

    internal static bool CanStartDesktopLifetime(System.Collections.IDictionary environmentVariables)
        => CanStartDesktopLifetime(
            environmentVariables,
            Environment.UserInteractive,
            environmentVariables["SESSIONNAME"]?.ToString());

    internal static bool CanStartDesktopLifetime(
        System.Collections.IDictionary environmentVariables,
        bool isUserInteractive,
        string? sessionName)
    {
        if (IsCiEnvironment(environmentVariables) || !OperatingSystem.IsWindows())
        {
            return false;
        }

        return isUserInteractive && IsSupportedWindowsSession(sessionName);
    }

    private static bool IsSupportedWindowsSession(string? sessionName)
        => !string.IsNullOrWhiteSpace(sessionName)
            && !sessionName.Equals("Services", StringComparison.OrdinalIgnoreCase)
            && SupportedWindowsSessionPrefixes.Any(prefix =>
                sessionName.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                || sessionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsCiEnvironment(System.Collections.IDictionary environmentVariables)
        => HasTruthyEnvironmentVariable(environmentVariables, "CI")
            || HasTruthyEnvironmentVariable(environmentVariables, "GITHUB_ACTIONS");

    private static bool HasTruthyEnvironmentVariable(System.Collections.IDictionary environmentVariables, string key)
        => TryGetEnvironmentVariable(environmentVariables, key, out var value)
            && (value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("1", StringComparison.OrdinalIgnoreCase));

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
