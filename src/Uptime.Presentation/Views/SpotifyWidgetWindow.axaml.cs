using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Uptime.Presentation.Views;

public partial class SpotifyWidgetWindow : Window
{
    public event Action? UserDismissed;

    public SpotifyWidgetWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            var screen = Screens.Primary;
            if (screen != null)
            {
                var work = screen.WorkingArea;
                var scale = screen.Scaling;
                var w = (int)Math.Ceiling(Width * scale);
                var h = (int)Math.Ceiling(Height * scale);
                var margin = (int)(24 * scale);
                Position = new PixelPoint(work.Right - w - margin, work.Bottom - h - margin);
            }
        }
        catch
        {
            // Default positioning fallback
        }
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button) return;
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        UserDismissed?.Invoke();
        Close();
    }
}
