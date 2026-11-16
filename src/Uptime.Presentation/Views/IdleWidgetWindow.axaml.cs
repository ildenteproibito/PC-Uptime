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
    private readonly DispatcherTimer _pixelShiftTimer = new() { Interval = TimeSpan.FromSeconds(45) };
    private readonly DispatcherTimer _bounceTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private int _shiftStep;
    private double _posX;
    private double _posY;
    private double _velX = 3.6;
    private double _velY = 2.65;
    private bool _bounceInitialized;
    private Point? _initialPointer;
    private bool _readyForInput;
    private bool _isFullscreen;

    public bool AutoCloseOnActivity { get; set; } = true;
    public Func<string?>? BatterySummaryProvider { get; set; }

    public void SetFullscreen(bool fullscreen)
    {
        _isFullscreen = fullscreen;
        ApplyFullscreenVisuals();
    }

    public IdleWidgetWindow()
    {
        InitializeComponent();
        ApplyFullscreenVisuals();
        _clockTimer.Tick += (_, _) => UpdateClock();
        UpdateClock();
        _clockTimer.Start();

        _pixelShiftTimer.Tick += (_, _) => ApplyOledPixelShift();
        _pixelShiftTimer.Start();

        BounceArea.SizeChanged += (_, _) => EnsureBounceBounds();
        _bounceTimer.Tick += (_, _) => StepBounce();
        _bounceTimer.Start();

        // Small delay to prevent cursor movement on window opening from immediately closing it
        _armTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _armTimer.Tick += (_, _) =>
        {
            _readyForInput = true;
            _armTimer?.Stop();
        };
        _armTimer.Start();
    }

    private DispatcherTimer? _armTimer;

    private void ApplyOledPixelShift()
    {
        _shiftStep = (_shiftStep + 1) % 4;
        var (x, y) = _shiftStep switch
        {
            0 => (0, 0),
            1 => (2, 1),
            2 => (1, -2),
            _ => (-2, 1)
        };
        ContentRoot.Margin = new Thickness(x, y, -x, -y);
    }

    private void EnsureBounceBounds()
    {
        var areaW = BounceArea.Bounds.Width;
        var areaH = BounceArea.Bounds.Height;
        var blockW = BounceBlock.Bounds.Width;
        var blockH = BounceBlock.Bounds.Height;
        if (areaW <= 0 || areaH <= 0 || blockW <= 0 || blockH <= 0) return;

        var maxX = Math.Max(0, areaW - blockW);
        var maxY = Math.Max(0, areaH - blockH);

        if (!_bounceInitialized)
        {
            _posX = maxX / 2.0;
            _posY = maxY / 2.0;
            _bounceInitialized = true;
        }
        else
        {
            _posX = Math.Clamp(_posX, 0, maxX);
            _posY = Math.Clamp(_posY, 0, maxY);
        }

        Canvas.SetLeft(BounceBlock, _posX);
        Canvas.SetTop(BounceBlock, _posY);
    }

    private void StepBounce()
    {
        var areaW = BounceArea.Bounds.Width;
        var areaH = BounceArea.Bounds.Height;
        var blockW = BounceBlock.Bounds.Width;
        var blockH = BounceBlock.Bounds.Height;
        if (areaW <= 0 || areaH <= 0 || blockW <= 0 || blockH <= 0) return;

        if (!_bounceInitialized)
        {
            EnsureBounceBounds();
            return;
        }

        var maxX = Math.Max(0, areaW - blockW);
        var maxY = Math.Max(0, areaH - blockH);

        _posX += _velX;
        _posY += _velY;

        if (_posX <= 0)
        {
            _posX = 0;
            _velX = Math.Abs(_velX);
        }
        else if (_posX >= maxX)
        {
            _posX = maxX;
            _velX = -Math.Abs(_velX);
        }

        if (_posY <= 0)
        {
            _posY = 0;
            _velY = Math.Abs(_velY);
        }
        else if (_posY >= maxY)
        {
            _posY = maxY;
            _velY = -Math.Abs(_velY);
        }

        Canvas.SetLeft(BounceBlock, _posX);
        Canvas.SetTop(BounceBlock, _posY);
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm");
        if (DataContext is MonitorViewModel vm)
        {
            UptimeSummaryText.Text = $"{vm.PrimaryText} {vm.SecondaryText}";
        }

        var bat = BatterySummaryProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(bat))
        {
            BatteryBadge.IsVisible = true;
            BatteryBadgeText.Text = bat;
        }
        else
        {
            BatteryBadge.IsVisible = false;
        }
    }

    private void OnActivityPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!AutoCloseOnActivity || !_readyForInput) return;
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
        if (e.Key == Key.Escape || (AutoCloseOnActivity && _readyForInput))
        {
            Close();
        }
    }

    private void OnToggleFullscreenClick(object? sender, RoutedEventArgs e)
    {
        _isFullscreen = !_isFullscreen;
        ApplyFullscreenVisuals();
    }

    private void ApplyFullscreenVisuals()
    {
        if (_isFullscreen)
        {
            WindowState = WindowState.FullScreen;
            RootBorder.CornerRadius = new CornerRadius(0);
            RootBorder.BorderThickness = new Thickness(0);
            FullscreenToggleBtn.Content = Uptime.Core.Localizer.Instance["StandByWindowed"];
        }
        else
        {
            WindowState = WindowState.Normal;
            RootBorder.CornerRadius = new CornerRadius(16);
            RootBorder.BorderThickness = new Thickness(1);
            FullscreenToggleBtn.Content = Uptime.Core.Localizer.Instance["StandByFullscreen"];
        }
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
        _armTimer?.Stop();
        _clockTimer.Stop();
        _pixelShiftTimer.Stop();
        _bounceTimer.Stop();
        base.OnClosed(e);
    }
}
