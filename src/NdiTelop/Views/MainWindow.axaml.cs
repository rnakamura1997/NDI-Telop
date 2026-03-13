using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using NdiTelop.Models;
using NdiTelop.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using Serilog;

namespace NdiTelop.Views;

public partial class MainWindow : Window
{
    private const string PresetDragDataFormat = "application/x-nditelop-preset-id";
    private Preset? _dragSourcePreset;

    public MainWindow()
    {
        InitializeComponent();

        var presetListBox = this.FindControl<ListBox>("PresetListBox");
        if (presetListBox != null)
        {
            DragDrop.SetAllowDrop(presetListBox, true);
            presetListBox.AddHandler(InputElement.PointerPressedEvent, PresetListBox_OnPointerPressed, RoutingStrategies.Bubble);
            presetListBox.AddHandler(DragDrop.DragOverEvent, PresetListBox_OnDragOver, RoutingStrategies.Bubble);
            presetListBox.AddHandler(DragDrop.DropEvent, PresetListBox_OnDrop, RoutingStrategies.Bubble);
        }
        // DataContextが設定された後にLoadPresetsAsyncを呼び出す
        this.Opened += (sender, e) =>
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                viewModel.LoadPresetsAsync().FireAndForget();
            }
        };
    }


    private void OpenSettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settingsWindow = Program.Services.GetRequiredService<SettingsWindow>();
        settingsWindow.DataContext = Program.Services.GetRequiredService<SettingsWindowViewModel>();
        settingsWindow.Show();
    }

    private async void PresetListBox_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var preset = ExtractPresetFromEventSource(e.Source);
        if (preset == null)
        {
            return;
        }

        _dragSourcePreset = preset;

        var dataObject = new DataObject();
        dataObject.Set(PresetDragDataFormat, preset.Id);
        dataObject.Set(DataFormats.Text, preset.Id);

        await DragDrop.DoDragDrop(e, dataObject, DragDropEffects.Move);
    }

    private void PresetListBox_OnDragOver(object? sender, DragEventArgs e)
    {
        var targetPreset = ExtractPresetFromEventSource(e.Source);
        if (_dragSourcePreset == null || targetPreset == null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        if (!e.Data.Contains(PresetDragDataFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void PresetListBox_OnDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var targetPreset = ExtractPresetFromEventSource(e.Source);
            if (targetPreset == null)
            {
                return;
            }

            if (_dragSourcePreset == null || _dragSourcePreset.Id == targetPreset.Id)
            {
                return;
            }

            var targetIndex = viewModel.Presets.ToList().FindIndex(p => p.Id == targetPreset.Id);
            if (targetIndex < 0)
            {
                return;
            }

            await viewModel.MovePresetAsync(_dragSourcePreset.Id, targetIndex);
            viewModel.SelectedPreset = _dragSourcePreset;
            e.Handled = true;
        }
        finally
        {
            _dragSourcePreset = null;
        }
    }

    private static Preset? ExtractPresetFromEventSource(object? source)
    {
        var current = source as Control;
        while (current != null)
        {
            if (current.DataContext is Preset preset)
            {
                return preset;
            }

            current = current.Parent as Control;
        }

        return null;
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var number = e.Key switch
        {
            Key.NumPad1 => 1,
            Key.NumPad2 => 2,
            Key.NumPad3 => 3,
            Key.NumPad4 => 4,
            Key.NumPad5 => 5,
            Key.NumPad6 => 6,
            Key.NumPad7 => 7,
            Key.NumPad8 => 8,
            Key.NumPad9 => 9,
            _ => 0
        };

        if (number == 0)
        {
            return;
        }

        await viewModel.TriggerPresetByNumberAsync(number);
        e.Handled = true;
    }


    private async void ImportPresetsButton_OnClick(object? sender, RoutedEventArgs e)
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

    private async void ExportSelectedPresetButton_OnClick(object? sender, RoutedEventArgs e)
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

    private async void ExportAllPresetsButton_OnClick(object? sender, RoutedEventArgs e)
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

    private async void ImportImageButton_OnClick(object? sender, RoutedEventArgs e)
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
