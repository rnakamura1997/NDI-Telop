using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace NdiTelop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // DataContextが設定された後にLoadPresetsAsyncを呼び出す
        this.Opened += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.LoadPresetsAsync().FireAndForget();
            }
        };
    }


    private void OpenSettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var settingsWindow = Program.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.DataContext = Program.Services.GetRequiredService<SettingsWindowViewModel>();
        settingsWindow.Show();
    }


    private async void ImportPresetsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "プリセットをインポート",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Preset JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        var selected = files.FirstOrDefault();
        if (selected == null)
        {
            return;
        }

        await viewModel.ImportPresetsAsync(selected.Path.LocalPath);
    }

    private async void ExportSelectedPresetButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || viewModel.SelectedPreset == null)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "選択プリセットをエクスポート",
            SuggestedFileName = $"preset_{viewModel.SelectedPreset.Name}.json",
            FileTypeChoices =
            [
                new FilePickerFileType("Preset JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (file == null)
        {
            return;
        }

        await viewModel.ExportSelectedPresetAsync(file.Path.LocalPath);
    }

    private async void ExportAllPresetsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "全プリセットをエクスポート",
            SuggestedFileName = "presets_export.json",
            FileTypeChoices =
            [
                new FilePickerFileType("Preset JSON")
                {
                    Patterns = ["*.json"]
                }
            ]
        });

        if (file == null)
        {
            return;
        }

        await viewModel.ExportAllPresetsAsync(file.Path.LocalPath);
    }

    private async void ImportImageButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "画像をインポート",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Image Files")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif", "*.webp" }
                }
            }
        });

        var selectedFile = files.FirstOrDefault();
        if (selectedFile == null)
        {
            return;
        }

        await viewModel.ImportOverlayImageAsync(selectedFile.Path.LocalPath);
    }
}

// FireAndForget拡張メソッド (Taskを非同期で実行し、結果を待たない)
// AvaloniaUIのイベントハンドラでasync voidを避けるため
internal static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.Exception != null)
            {
                Log.Error(t.Exception.Flatten(), "Unhandled exception in fire-and-forget task.");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
