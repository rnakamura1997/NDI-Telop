using Avalonia.Platform.Storage;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NdiTelop.ViewModels;

namespace NdiTelop.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            if (DataContext is SettingsWindowViewModel vm)
            {
                vm.LoadCommand.Execute(null);
            }
        };
    }

    private async void CreateBackupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel vm)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "バックアップを保存",
            SuggestedFileName = $"nditelop_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip",
            FileTypeChoices =
            [
                new FilePickerFileType("Zip Archive")
                {
                    Patterns = ["*.zip"]
                }
            ]
        });

        if (file != null)
        {
            await vm.CreateBackupAsync(file.Path.LocalPath);
        }
    }

    private async void RestoreBackupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsWindowViewModel vm)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "バックアップを復元",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Zip Archive")
                {
                    Patterns = ["*.zip"]
                }
            ]
        });

        var selected = files.FirstOrDefault();
        if (selected != null)
        {
            await vm.RestoreBackupAsync(selected.Path.LocalPath);
        }
    }
}
