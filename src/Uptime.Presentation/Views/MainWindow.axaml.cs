using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Uptime.Presentation;

namespace Uptime.Presentation.Views;

public partial class MainWindow : Window
{
    private GlobalHotkeyService? _hotkey;
    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => _hotkey = new GlobalHotkeyService(ToggleVisibility);
        Closed += (_, _) => { _hotkey?.Dispose(); _hotkey = null; };
    }

    private void OnWindowDragPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is Button or ComboBox or TextBox or ToggleSwitch) return;
        }
        BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnToggleMaximizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeIcon.IsVisible = WindowState != WindowState.Maximized;
        RestoreIcon.IsVisible = WindowState == WindowState.Maximized;
    }
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private void ToggleVisibility()
    {
        if (IsVisible) Hide();
        else { Show(); Activate(); }
    }
}
