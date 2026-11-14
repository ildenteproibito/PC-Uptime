using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class TrayMenuWindow : Window
{
    private Action _open = static () => { };
    private Action _settings = static () => { };
    private Action _exit = static () => { };
    private Action _standBy = static () => { };
    private Action _assistant = static () => { };

    public TrayMenuWindow() => InitializeComponent();

    public TrayMenuWindow(
        MonitorViewModel monitor,
        Action open,
        Action settings,
        Action exit,
        Action? standBy = null,
        Action? assistant = null) : this()
    {
        DataContext = monitor;
        _open = open;
        _settings = settings;
        _exit = exit;
        _standBy = standBy ?? (() => { });
        _assistant = assistant ?? (() => { });
        Deactivated += (_, _) => Close();
    }

    private void OnDismissClick(object? sender, RoutedEventArgs e) => Close();
    private void OnOpenClick(object? sender, RoutedEventArgs e) { Close(); _open(); }
    private void OnSettingsClick(object? sender, RoutedEventArgs e) { Close(); _settings(); }
    private void OnStandByClick(object? sender, RoutedEventArgs e) { Close(); _standBy(); }
    private void OnAssistantClick(object? sender, RoutedEventArgs e) { Close(); _assistant(); }
    private void OnExitClick(object? sender, RoutedEventArgs e) { Close(); _exit(); }
}
