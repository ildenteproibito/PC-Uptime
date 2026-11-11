using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class MonitorView : UserControl
{
    public MonitorView() => InitializeComponent();

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(vm.ClipboardText);
    }

    private async void OnBadgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(vm.L.Format("BadgeTemplate", $"{vm.PrimaryText} {vm.SecondaryText}", vm.StatusText));
    }

    private MiniHudWindow? _hudWindow;

    private void OnHudClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MonitorViewModel vm) return;
        if (_hudWindow is { IsVisible: true })
        {
            if (_hudWindow.WindowState == WindowState.Minimized)
                _hudWindow.WindowState = WindowState.Normal;
            _hudWindow.Activate();
        }
        else
        {
            _hudWindow = new MiniHudWindow { DataContext = vm };
            _hudWindow.Closed += (_, _) => _hudWindow = null;
            _hudWindow.Show();
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && window.DataContext is MainViewModel main)
            main.OpenSettingsCommand.Execute(null);
    }
}
