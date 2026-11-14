using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Uptime.Presentation.Views;

public partial class MainWindow : Window
{
    private GlobalHotkeyService? _hotkey;
    private bool _isDraggingMascot;
    private bool _didMoveMascot;
    private bool _hasCustomMascotPosition;
    private Point _mascotDragOffset;
    private Point _mascotPressStart;
    private double _mascotDragTiltTarget;
    private double _mascotDragTiltCurrent;
    private double _lastMascotDragX;
    private double _lastMascotDragY;
    private double _mascotFlingVelX;
    private double _mascotFlingVelY;
    private bool _bouncedThisFling;
    private double _petAccumulatedDist;
    private Point _lastPetPos;
    private bool _hasLastPetPos;
    private readonly List<HeartParticle> _activeHearts = new();
    private long _mascotPokeStartTick;

    private sealed class HeartParticle
    {
        public required TextBlock Control { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Vy { get; set; }
        public double Phase { get; set; }
        public double Age { get; set; }
        public double Lifetime { get; set; }
    }
    private readonly Stopwatch _mascotClock = Stopwatch.StartNew();
    private readonly ScaleTransform _mascotScale = new();
    private readonly RotateTransform _mascotRotate = new();
    private readonly TranslateTransform _mascotTranslate = new();
    private DispatcherTimer? _mascotAnimTimer;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) =>
        {
            _hotkey?.Dispose();
            _hotkey = new GlobalHotkeyService(ToggleVisibility);
            UpdateHeaderCentering();
            AnchorMascotBottomRight();
            StartMascotAnimation();
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.Monitor.RequestMascotCelebrate -= TriggerMascotCelebrate;
                vm.Monitor.RequestMascotHearts -= SpawnMascotHearts;
                vm.Monitor.RequestMascotCelebrate += TriggerMascotCelebrate;
                vm.Monitor.RequestMascotHearts += SpawnMascotHearts;
            }
        };
        Closed += (_, _) =>
        {
            if (DataContext is ViewModels.MainViewModel vm)
            {
                vm.Monitor.RequestMascotCelebrate -= TriggerMascotCelebrate;
                vm.Monitor.RequestMascotHearts -= SpawnMascotHearts;
            }
            _mascotAnimTimer?.Stop();
            _mascotAnimTimer = null;
            _hotkey?.Dispose();
            _hotkey = null;
        };
        SizeChanged += (_, _) => UpdateHeaderCentering();
        NavTabsPanel.SizeChanged += (_, _) => UpdateHeaderCentering();
        HeaderLeftPanel.SizeChanged += (_, _) => UpdateHeaderCentering();
        HeaderRightPanel.SizeChanged += (_, _) => UpdateHeaderCentering();
        MascotLayer.SizeChanged += (_, _) =>
        {
            if (!_hasCustomMascotPosition) AnchorMascotBottomRight();
            else ClampMascotPosition();
        };
        MascotBubbleBorder.SizeChanged += (_, _) => UpdateMascotBubblePlacement();
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && DataContext is ViewModels.MainViewModel vm && vm.IsWallpaperFocusActive)
            {
                vm.Monitor.IsWallpaperFocus = false;
                e.Handled = true;
            }
        };
    }

    private void AnchorMascotBottomRight()
    {
        var layerW = MascotLayer.Bounds.Width > 0 ? MascotLayer.Bounds.Width : Width;
        var layerH = MascotLayer.Bounds.Height > 0 ? MascotLayer.Bounds.Height : Math.Max(200, Height - 56);
        var mw = MascotOverlay.Bounds.Width > 0 ? MascotOverlay.Bounds.Width : 124;
        var mh = MascotOverlay.Bounds.Height > 0 ? MascotOverlay.Bounds.Height : 158;

        Canvas.SetLeft(MascotOverlay, Math.Max(0, layerW - mw - 24));
        Canvas.SetTop(MascotOverlay, Math.Max(0, layerH - mh - 20));
        UpdateMascotBubblePlacement();
    }

    private void ClampMascotPosition()
    {
        var layerW = MascotLayer.Bounds.Width > 0 ? MascotLayer.Bounds.Width : Width;
        var layerH = MascotLayer.Bounds.Height > 0 ? MascotLayer.Bounds.Height : Math.Max(200, Height - 56);
        var mw = MascotOverlay.Bounds.Width > 0 ? MascotOverlay.Bounds.Width : 124;
        var mh = MascotOverlay.Bounds.Height > 0 ? MascotOverlay.Bounds.Height : 158;

        var left = Canvas.GetLeft(MascotOverlay);
        var top = Canvas.GetTop(MascotOverlay);
        if (double.IsNaN(left)) left = Math.Max(0, layerW - mw - 24);
        if (double.IsNaN(top)) top = Math.Max(0, layerH - mh - 20);

        Canvas.SetLeft(MascotOverlay, Math.Clamp(left, 0, Math.Max(0, layerW - mw)));
        Canvas.SetTop(MascotOverlay, Math.Clamp(top, 0, Math.Max(0, layerH - mh)));
        UpdateMascotBubblePlacement();
    }

    private void UpdateMascotBubblePlacement()
    {
        var bw = MascotBubbleBorder.Bounds.Width;
        var bh = MascotBubbleBorder.Bounds.Height;
        if (bw <= 0 || bh <= 0) return;

        var layerW = MascotLayer.Bounds.Width > 0 ? MascotLayer.Bounds.Width : Width;
        var layerH = MascotLayer.Bounds.Height > 0 ? MascotLayer.Bounds.Height : Math.Max(200, Height - 56);
        var mw = MascotOverlay.Bounds.Width > 0 ? MascotOverlay.Bounds.Width : 124;
        var mh = MascotOverlay.Bounds.Height > 0 ? MascotOverlay.Bounds.Height : 158;

        var left = Canvas.GetLeft(MascotOverlay);
        var top = Canvas.GetTop(MascotOverlay);
        if (double.IsNaN(left)) left = Math.Max(0, layerW - mw - 24);
        if (double.IsNaN(top)) top = Math.Max(0, layerH - mh - 20);

        const double edgePad = 8;
        var idealGlobalLeft = left + (mw - bw) / 2.0;
        var clampedGlobalLeft = Math.Clamp(idealGlobalLeft, edgePad, Math.Max(edgePad, layerW - bw - edgePad));
        var canvasLeft = clampedGlobalLeft - (left + mw / 2.0);

        var aboveGlobalTop = top - 6 - bh;
        var belowGlobalTop = top + mh + 6;
        double canvasTop;
        if (aboveGlobalTop >= edgePad || belowGlobalTop + bh > layerH - edgePad)
            canvasTop = Math.Max(edgePad, aboveGlobalTop) - top;
        else
            canvasTop = mh + 6;

        Canvas.SetLeft(MascotBubbleBorder, canvasLeft);
        Canvas.SetTop(MascotBubbleBorder, canvasTop);
    }

    private void OnMascotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        _isDraggingMascot = true;
        _didMoveMascot = false;
        var posInLayer = e.GetPosition(MascotLayer);
        _mascotPressStart = posInLayer;
        _lastMascotDragX = posInLayer.X;
        _lastMascotDragY = posInLayer.Y;
        _mascotFlingVelX = 0;
        _mascotFlingVelY = 0;
        _bouncedThisFling = false;
        var left = Canvas.GetLeft(MascotOverlay);
        var top = Canvas.GetTop(MascotOverlay);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;
        _mascotDragOffset = new Point(posInLayer.X - left, posInLayer.Y - top);
        e.Pointer.Capture(MascotOverlay);
    }

    private void OnMascotPointerMoved(object? sender, PointerEventArgs e)
    {
        var posInLayer = e.GetPosition(MascotLayer);
        if (!_isDraggingMascot)
        {
            if (_hasLastPetPos)
            {
                var pdx = posInLayer.X - _lastPetPos.X;
                var pdy = posInLayer.Y - _lastPetPos.Y;
                _petAccumulatedDist += Math.Sqrt(pdx * pdx + pdy * pdy);
                if (_petAccumulatedDist >= 110)
                {
                    _petAccumulatedDist = 0;
                    if (DataContext is ViewModels.MainViewModel petVm)
                        petVm.Monitor.PetMascot();
                }
            }
            _lastPetPos = posInLayer;
            _hasLastPetPos = true;
            return;
        }

        _hasLastPetPos = false;
        var dx = posInLayer.X - _mascotPressStart.X;
        var dy = posInLayer.Y - _mascotPressStart.Y;
        if (!_didMoveMascot && (dx * dx + dy * dy) >= 25)
        {
            _didMoveMascot = true;
            if (DataContext is ViewModels.MainViewModel vm)
                vm.Monitor.SetMascotDragging(true);
        }
        if (_didMoveMascot)
        {
            var deltaX = posInLayer.X - _lastMascotDragX;
            var deltaY = posInLayer.Y - _lastMascotDragY;
            _lastMascotDragX = posInLayer.X;
            _lastMascotDragY = posInLayer.Y;
            _mascotFlingVelX = Math.Clamp(deltaX * 0.7 + _mascotFlingVelX * 0.3, -32, 32);
            _mascotFlingVelY = Math.Clamp(deltaY * 0.7 + _mascotFlingVelY * 0.3, -32, 32);
            _mascotDragTiltTarget = Math.Clamp(deltaX * 0.85, -14, 14);
            Canvas.SetLeft(MascotOverlay, posInLayer.X - _mascotDragOffset.X);
            Canvas.SetTop(MascotOverlay, posInLayer.Y - _mascotDragOffset.Y);
            ClampMascotPosition();
        }
    }

    private void OnMascotPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingMascot) return;
        _isDraggingMascot = false;
        _mascotDragTiltTarget = 0;
        e.Pointer.Capture(null);
        if (_didMoveMascot)
        {
            _hasCustomMascotPosition = true;
            if (DataContext is ViewModels.MainViewModel vm)
                vm.Monitor.SetMascotDragging(false);
            if (Math.Abs(_mascotFlingVelX) < 3.5 && Math.Abs(_mascotFlingVelY) < 3.5)
            {
                _mascotFlingVelX = 0;
                _mascotFlingVelY = 0;
                SaveMascotPosition();
            }
        }
        else if (DataContext is ViewModels.MainViewModel vm)
        {
            _mascotPokeStartTick = _mascotClock.ElapsedMilliseconds;
            vm.Monitor.PokeMascot();
        }
    }

    private void OnMascotPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!_isDraggingMascot) return;
        _isDraggingMascot = false;
        _mascotDragTiltTarget = 0;
        if (_didMoveMascot)
        {
            _hasCustomMascotPosition = true;
            if (DataContext is ViewModels.MainViewModel vm)
                vm.Monitor.SetMascotDragging(false);
            SaveMascotPosition();
        }
    }

    private void TriggerMascotCelebrate()
    {
        _mascotPokeStartTick = _mascotClock.ElapsedMilliseconds;
    }

    private void StartMascotAnimation()
    {
        if (_mascotAnimTimer is not null) return;
        MascotImageControl.RenderTransform = new TransformGroup
        {
            Children = { _mascotScale, _mascotRotate, _mascotTranslate }
        };
        _mascotAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _mascotAnimTimer.Tick += (_, _) => UpdateMascotAnimation();
        _mascotAnimTimer.Start();
    }

    private void UpdateMascotAnimation()
    {
        if (!IsVisible || WindowState == WindowState.Minimized) return;
        if (DataContext is not ViewModels.MainViewModel vm || !vm.Monitor.ShowMascot) return;

        var t = _mascotClock.Elapsed.TotalSeconds;
        var mood = vm.Monitor.MascotMoodKey;

        double yOffset;
        double angle;
        double scaleX;
        double scaleY;

        switch (mood)
        {
            case "spotify":
                // Rhythmic musical bounce & groove tilt
                yOffset = -Math.Abs(Math.Sin(t * 4.4)) * 6.0;
                angle = Math.Sin(t * 2.2) * 5.0;
                var groove = Math.Sin(t * 4.4) * 0.03;
                scaleX = 1.0 - groove * 0.5;
                scaleY = 1.0 + groove;
                break;

            case "coffee":
                // Energetic lively bobbing
                yOffset = Math.Sin(t * 4.6) * 3.8;
                angle = Math.Sin(t * 3.2) * 3.2;
                scaleX = 1.0 + Math.Sin(t * 4.6) * 0.025;
                scaleY = 1.0 + Math.Cos(t * 4.6) * 0.025;
                break;

            case "critical":
                // Periodic alert pulse / micro-shiver
                var pulse = Math.Max(0, Math.Sin(t * 2.4));
                var shiver = Math.Sin(t * 26.0) * pulse * pulse;
                yOffset = Math.Sin(t * 3.0) * 2.5 - pulse * 2.0;
                angle = shiver * 3.8;
                scaleX = 1.0 + pulse * 0.035;
                scaleY = 1.0 + pulse * 0.035;
                break;

            case "idle":
            case "attention":
                // Slow sleepy breathing & gentle sway
                yOffset = Math.Sin(t * 1.25) * 3.2;
                angle = Math.Sin(t * 0.85) * 2.4;
                var breath = Math.Sin(t * 1.25) * 0.022;
                scaleX = 1.0 - breath * 0.4;
                scaleY = 1.0 + breath;
                break;

            default:
                // "good": serene floating & gentle breathing
                yOffset = Math.Sin(t * 2.0) * 4.2;
                angle = Math.Sin(t * 1.2) * 2.6;
                var happy = Math.Sin(t * 2.0) * 0.022;
                scaleX = 1.0 + happy;
                scaleY = 1.0 + happy;
                break;
        }

        // Smooth cushioned squash-and-stretch reaction when poked (60fps friendly)
        if (_mascotPokeStartTick > 0)
        {
            var dt = (_mascotClock.ElapsedMilliseconds - _mascotPokeStartTick) / 1000.0;
            if (dt < 0.55)
            {
                var p = dt / 0.55;
                var smoothLift = Math.Sin(p * Math.PI);
                smoothLift *= smoothLift; // zero derivative at start & end
                var damped = Math.Exp(-4.2 * p) * Math.Sin(p * Math.PI * 2.0);
                yOffset -= smoothLift * 11.0;
                scaleX += -damped * 0.085;
                scaleY += damped * 0.095;
                angle += damped * 4.5;
            }
            else
            {
                _mascotPokeStartTick = 0;
            }
        }

        // Smooth lift & velocity tilt while dragging
        _mascotDragTiltCurrent += (_mascotDragTiltTarget - _mascotDragTiltCurrent) * 0.16;
        if (_isDraggingMascot && _didMoveMascot)
        {
            _mascotDragTiltTarget *= 0.92;
            scaleX *= 1.06;
            scaleY *= 1.06;
            yOffset -= 4.0;
        }

        _mascotTranslate.Y = yOffset;
        _mascotRotate.Angle = angle + _mascotDragTiltCurrent;
        _mascotScale.ScaleX = scaleX;
        _mascotScale.ScaleY = scaleY;

        UpdateMascotFling(vm);
        UpdateMascotHearts();
    }

    private void UpdateMascotFling(ViewModels.MainViewModel vm)
    {
        if (_isDraggingMascot || (Math.Abs(_mascotFlingVelX) < 0.2 && Math.Abs(_mascotFlingVelY) < 0.2))
            return;

        var layerW = MascotLayer.Bounds.Width;
        var layerH = MascotLayer.Bounds.Height;
        var mw = MascotOverlay.Bounds.Width > 1 ? MascotOverlay.Bounds.Width : 140;
        var mh = MascotOverlay.Bounds.Height > 1 ? MascotOverlay.Bounds.Height : 155;
        if (layerW <= 1 || layerH <= 1) return;

        var left = Canvas.GetLeft(MascotOverlay);
        var top = Canvas.GetTop(MascotOverlay);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        left += _mascotFlingVelX;
        top += _mascotFlingVelY;
        _mascotFlingVelX *= 0.93;
        _mascotFlingVelY *= 0.93;

        var maxLeft = Math.Max(0, layerW - mw);
        var maxTop = Math.Max(0, layerH - mh);
        var hitEdge = false;

        if (left < 0) { left = 0; _mascotFlingVelX = Math.Abs(_mascotFlingVelX) * 0.72; hitEdge = true; }
        else if (left > maxLeft) { left = maxLeft; _mascotFlingVelX = -Math.Abs(_mascotFlingVelX) * 0.72; hitEdge = true; }

        if (top < 0) { top = 0; _mascotFlingVelY = Math.Abs(_mascotFlingVelY) * 0.72; hitEdge = true; }
        else if (top > maxTop) { top = maxTop; _mascotFlingVelY = -Math.Abs(_mascotFlingVelY) * 0.72; hitEdge = true; }

        if (hitEdge && !_bouncedThisFling)
        {
            _bouncedThisFling = true;
            vm.Monitor.NotifyMascotBounce();
        }

        Canvas.SetLeft(MascotOverlay, left);
        Canvas.SetTop(MascotOverlay, top);
        ClampMascotPosition();

        if (Math.Abs(_mascotFlingVelX) < 0.35 && Math.Abs(_mascotFlingVelY) < 0.35)
        {
            _mascotFlingVelX = 0;
            _mascotFlingVelY = 0;
            SaveMascotPosition();
        }
    }

    private void SpawnMascotHearts()
    {
        var left = Canvas.GetLeft(MascotOverlay);
        var top = Canvas.GetTop(MascotOverlay);
        if (double.IsNaN(left)) left = 0;
        if (double.IsNaN(top)) top = 0;

        var mw = MascotOverlay.Bounds.Width > 1 ? MascotOverlay.Bounds.Width : 124;
        var emojis = new[] { "💖", "💕", "❤️", "💗", "💓" };
        var rng = Random.Shared;

        for (int i = 0; i < 6; i++)
        {
            var tb = new TextBlock
            {
                Text = emojis[i % emojis.Length],
                FontSize = rng.Next(15, 22),
                IsHitTestVisible = false
            };

            var x = left + mw * 0.5 + rng.Next(-42, 43);
            var y = top + rng.Next(8, 52);

            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            MascotLayer.Children.Add(tb);

            _activeHearts.Add(new HeartParticle
            {
                Control = tb,
                X = x,
                Y = y,
                Vy = -(1.1 + rng.NextDouble() * 0.9),
                Phase = rng.NextDouble() * Math.PI * 2,
                Age = 0,
                Lifetime = 1.15 + rng.NextDouble() * 0.4
            });
        }
    }

    private void UpdateMascotHearts()
    {
        if (_activeHearts.Count == 0) return;

        const double dt = 0.016;
        for (int i = _activeHearts.Count - 1; i >= 0; i--)
        {
            var h = _activeHearts[i];
            h.Age += dt;
            if (h.Age >= h.Lifetime)
            {
                MascotLayer.Children.Remove(h.Control);
                _activeHearts.RemoveAt(i);
                continue;
            }

            h.Y += h.Vy;
            var sway = Math.Sin(h.Age * 7.5 + h.Phase) * 0.75;
            h.X += sway;

            var progress = h.Age / h.Lifetime;
            h.Control.Opacity = Math.Clamp(1.0 - progress * progress, 0, 1);
            Canvas.SetLeft(h.Control, h.X);
            Canvas.SetTop(h.Control, h.Y);
        }
    }

    private void SaveMascotPosition()
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            var left = Canvas.GetLeft(MascotOverlay);
            var top = Canvas.GetTop(MascotOverlay);
            if (!double.IsNaN(left) && !double.IsNaN(top))
                vm.Settings.SetMascotPosition(left, top);
        }
    }

    private void UpdateHeaderCentering()
    {
        var w = Bounds.Width;
        var navW = NavTabsPanel.Bounds.Width;
        var leftW = HeaderLeftPanel.Bounds.Width;
        var rightW = HeaderRightPanel.Bounds.Width;

        if (w <= 0 || navW <= 0 || rightW <= 0)
        {
            Grid.SetColumn(NavTabsPanel, 1);
            Grid.SetColumnSpan(NavTabsPanel, 1);
            return;
        }

        // Only mathematically center across the whole header if there is >= 24px clearance on both sides
        const double padding = 18;
        const double clearance = 24;
        var navLeft = (w - navW) * 0.5;
        var navRight = (w + navW) * 0.5;
        bool fitsCentered = navLeft >= padding + leftW + clearance &&
                            navRight <= w - padding - rightW - clearance;

        Grid.SetColumn(NavTabsPanel, fitsCentered ? 0 : 1);
        Grid.SetColumnSpan(NavTabsPanel, fitsCentered ? 3 : 1);
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
        else
        {
            if (WindowState == WindowState.Minimized)
                WindowState = WindowState.Normal;
            Show();
            Activate();
        }
    }
}
