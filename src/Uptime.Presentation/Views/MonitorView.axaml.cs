using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class MonitorView : UserControl
{
    public MonitorView() => InitializeComponent();

    private void OnHudClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && window.DataContext is MainViewModel main)
            main.ToggleDesktopWidgetCommand.Execute(null);
    }

    private void OnStandByClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && window.DataContext is MainViewModel main)
            main.OpenStandByMode();
    }

    private void OnToggleWallpaperFocusClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
            vm.IsWallpaperFocus = !vm.IsWallpaperFocus;
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow window && window.DataContext is MainViewModel main)
            main.OpenSettingsCommand.Execute(null);
    }
}
