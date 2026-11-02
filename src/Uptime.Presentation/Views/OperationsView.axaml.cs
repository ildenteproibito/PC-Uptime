using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class OperationsView : UserControl
{
    public OperationsView() => InitializeComponent();

    private async void OnFastStartupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperationsViewModel vm && sender is ToggleSwitch toggle)
            await vm.SetFastStartupAsync(toggle.IsChecked == true);
    }

    private void OnRunAtStartupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperationsViewModel vm && sender is ToggleSwitch toggle)
            vm.SetRunAtStartup(toggle.IsChecked == true);
    }
}
