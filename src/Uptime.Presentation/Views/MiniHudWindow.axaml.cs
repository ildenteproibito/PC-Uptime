using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Uptime.Presentation.Views;

public partial class MiniHudWindow : Window
{
    public MiniHudWindow() => InitializeComponent();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button) return;
        BeginMoveDrag(e);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
