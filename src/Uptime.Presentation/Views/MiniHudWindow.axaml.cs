using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Uptime.Presentation.Views;

public partial class MiniHudWindow : Window
{
    private bool _isPillMode;
    private bool _ghostMode = true;
    private bool _isHovered;

    public MiniHudWindow() => InitializeComponent();

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button) return;
        BeginMoveDrag(e);
    }

    private void OnWindowDoubleTapped(object? sender, TappedEventArgs e)
    {
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button) return;
        TogglePillMode();
    }

    private void OnWindowPointerEntered(object? sender, PointerEventArgs e)
    {
        _isHovered = true;
        UpdateOpacity();
    }

    private void OnWindowPointerExited(object? sender, PointerEventArgs e)
    {
        _isHovered = false;
        UpdateOpacity();
    }

    private void OnToggleGhostClick(object? sender, RoutedEventArgs e)
    {
        _ghostMode = !_ghostMode;
        GhostButton.Classes.Set("active", _ghostMode);
        UpdateOpacity();
    }

    private void OnTogglePillClick(object? sender, RoutedEventArgs e) => TogglePillMode();

    private void TogglePillMode()
    {
        _isPillMode = !_isPillMode;
        CardRoot.IsVisible = !_isPillMode;
        PillRoot.IsVisible = _isPillMode;

        if (_isPillMode)
        {
            Width = 276;
            Height = 42;
            HudShell.CornerRadius = new CornerRadius(21);
        }
        else
        {
            Width = 384;
            Height = 188;
            HudShell.CornerRadius = new CornerRadius(16);
        }
    }

    private void UpdateOpacity()
    {
        HudShell.Opacity = (_isHovered || !_ghostMode) ? 1.0 : 0.72;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
