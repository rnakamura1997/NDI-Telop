using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NdiTelop.Interfaces;
using NdiTelop.ViewModels;
using NdiTelop.Views;

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
            var themeService = Program.Services.GetRequiredService<Services.ThemeService>();
            themeService.ApplyTheme(settingsService.Settings.Theme);

            var window = Program.Services.GetRequiredService<MainWindow>();
            window.DataContext = Program.Services.GetRequiredService<MainWindowViewModel>();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
