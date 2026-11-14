using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class OperationsView : UserControl
{
    public OperationsView() => InitializeComponent();

    private async void OnFastStartupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not OperationsViewModel vm || sender is not ToggleSwitch toggle) return;
        try
        {
            await vm.SetFastStartupAsync(toggle.IsChecked == true);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to toggle Fast Startup", ex);
        }
    }

    private void OnRunAtStartupClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is OperationsViewModel vm && sender is ToggleSwitch toggle)
            vm.SetRunAtStartup(toggle.IsChecked == true);
    }
}
