using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class TrayMenuWindow : Window
{
    private Action _open = static () => { };
    private Action _settings = static () => { };
    private Action _exit = static () => { };

    public TrayMenuWindow() => InitializeComponent();

    public TrayMenuWindow(MonitorViewModel monitor, Action open, Action settings, Action exit) : this()
    {
        DataContext = monitor;
        _open = open;
        _settings = settings;
        _exit = exit;
        Deactivated += (_, _) => Close();
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Close();
    private void OnOpenClick(object? sender, RoutedEventArgs e) { Close(); _open(); }
    private void OnSettingsClick(object? sender, RoutedEventArgs e) { Close(); _settings(); }
    private void OnExitClick(object? sender, RoutedEventArgs e) { Close(); _exit(); }
}
