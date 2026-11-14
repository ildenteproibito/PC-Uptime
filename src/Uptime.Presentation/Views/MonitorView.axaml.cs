using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class MonitorView : UserControl
{
    public MonitorView() => InitializeComponent();

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
            await CopyToClipboardAsync(vm.ClipboardText);
    }

    private async void OnBadgeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
            await CopyToClipboardAsync(vm.L.Format("BadgeTemplate", $"{vm.PrimaryText} {vm.SecondaryText}", vm.StatusText));
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(text);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to copy text to clipboard", ex);
        }
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
