using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class IdleWidgetWindow : Window
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private Point? _initialPointer;
    private bool _readyForInput;

    public IdleWidgetWindow()
    {
        InitializeComponent();
        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clockTimer.Start();

        // Small delay to prevent cursor movement on window opening from immediately closing it
        var armTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        armTimer.Tick += (_, _) =>
        {
            _readyForInput = true;
            armTimer.Stop();
        };
        armTimer.Start();
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm");
        if (DataContext is MonitorViewModel vm)
        {
            UptimeSummaryText.Text = $"{vm.PrimaryText} {vm.SecondaryText}";
        }
    }

    private void OnActivityPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_readyForInput) return;
        var current = e.GetPosition(this);
        if (_initialPointer == null)
        {
            _initialPointer = current;
            return;
        }

        // If cursor moved more than 8 pixels, user activity resumed
        var dist = Math.Abs(current.X - _initialPointer.Value.X) + Math.Abs(current.Y - _initialPointer.Value.Y);
        if (dist > 8)
        {
            Close();
        }
    }

    private void OnActivityKeyDown(object? sender, KeyEventArgs e)
    {
        if (_readyForInput) Close();
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        for (var visual = e.Source as Visual; visual is not null && visual != this; visual = visual.GetVisualParent())
            if (visual is Button) return;
        BeginMoveDrag(e);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        _clockTimer.Stop();
        base.OnClosed(e);
    }
}
