using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Interfaces;
using NdiTelop.Services;
using NdiTelop.ViewModels;
using NdiTelop.Views;
using Serilog;

namespace NdiTelop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = Program.Services.GetRequiredService<ISettingsService>();
            var startupService = Program.Services.GetRequiredService<ApplicationStartupService>();
            var themeService = Program.Services.GetRequiredService<ThemeService>();

            settingsService.LoadAsync().GetAwaiter().GetResult();
            themeService.ApplyTheme(settingsService.Settings.Theme);

            var window = Program.Services.GetRequiredService<MainWindow>();
            window.DataContext = Program.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = window;
            desktop.MainWindow.Opened += async (_, _) =>
            {
                try
                {
                    await startupService.InitializeAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Deferred application startup initialization failed.");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
