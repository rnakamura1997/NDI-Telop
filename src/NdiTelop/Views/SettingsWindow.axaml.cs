using Avalonia.Controls;
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
}
