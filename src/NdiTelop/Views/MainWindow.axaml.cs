using Avalonia.Controls;
using NdiTelop.ViewModels;
using System.Threading.Tasks;

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
}

// FireAndForget拡張メソッド (Taskを非同期で実行し、結果を待たない)
// AvaloniaUIのイベントハンドラでasync voidを避けるため
internal static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        // エラーハンドリングは別途考慮が必要
        // ここでは単純に例外を無視する
    }
}
