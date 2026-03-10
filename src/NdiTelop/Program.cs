using Avalonia;
using Avalonia.ReactiveUI;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Interfaces;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using NdiTelop.Views;

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

        services.AddSingleton<INdiService, NdiService>();
        services.AddSingleton<IRenderService, RenderService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<ISetlistService, SetlistService>();
        services.AddSingleton<IWebApiService, WebApiService>();
        services.AddSingleton<IOscService, OscService>();
        services.AddSingleton<IOutputService, OutputService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        return services.BuildServiceProvider();
    }
}
