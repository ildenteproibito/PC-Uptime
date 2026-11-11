using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Uptime.Core;
using Uptime.Infrastructure;
using Uptime.Presentation.ViewModels;
using Uptime.Presentation.Views;

namespace Uptime.Presentation;

public partial class App : Application
{
    private bool _exitRequested;
    private System.Windows.Forms.NotifyIcon? _uptimeTrayIcon;
    private System.Drawing.Icon? _trayImage;
    private TrayMenuWindow? _trayMenuWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDiagnostics.Write("Information", $"Uptime starting. Version {typeof(App).Assembly.GetName().Version}; Windows {Environment.OSVersion}; language {Localizer.Instance.Language}.");
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            AppDiagnostics.Write("Fatal", $"Unhandled exception (terminating={args.IsTerminating}).", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppDiagnostics.Write("Error", "Unobserved task exception.", args.Exception);
            args.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, args) =>
            AppDiagnostics.Write("Error", "Unhandled UI dispatcher exception.", args.Exception);
        using (var iconStream = AssetLoader.Open(new Uri("avares://Uptime/Assets/uptime.ico")))
            _trayImage = new System.Drawing.Icon(iconStream);
        _uptimeTrayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = _trayImage,
            Text = Localizer.Instance["TrayTooltip"],
            Visible = true
        };
        _uptimeTrayIcon.MouseUp += OnTrayMouseUp;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        UpdateTrayLanguage();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(
                    new Win32UptimeProvider(),
                    new Win32IdleProvider(),
                    new WindowsEventLogService(),
                    new Win32PowerService(),
                    new Win32StartupService(),
                    new WindowsBatteryService(),
                    new WindowsSleepBlockerService(),
                    new WindowsStartupAppsService(),
                    new WindowsMediaService())
            };
            desktop.MainWindow.Closing += (_, e) =>
            {
                if (_exitRequested) return;
                e.Cancel = true;
                desktop.MainWindow.Hide();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void ShowMainWindow_OnClick(object? sender, EventArgs e)
    {
        _trayMenuWindow?.Close();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } window })
        {
            window.Show();
            window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    private void Exit_OnClick(object? sender, EventArgs e)
    {
        _exitRequested = true;
        DisposeTrayIcon();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void OnTrayMouseUp(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        if (e.Button == System.Windows.Forms.MouseButtons.Right)
            Dispatcher.UIThread.Post(() => ShowTrayMenu(new PixelPoint(cursor.X, cursor.Y)));
        else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            Dispatcher.UIThread.Post(() => ShowMainWindow_OnClick(sender, EventArgs.Empty));
    }

    private void ShowTrayMenu(PixelPoint pointer)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not { DataContext: MainViewModel main } owner) return;

        _trayMenuWindow?.Close();
        var menu = new TrayMenuWindow(main.Monitor,
            () => ShowMainWindow_OnClick(this, EventArgs.Empty), OpenSettingsFromTray,
            () => Exit_OnClick(this, EventArgs.Empty));
        var screen = owner.Screens.ScreenFromPoint(pointer);
        var scale = screen?.Scaling ?? 1;
        var work = screen?.WorkingArea;
        var width = (int)Math.Ceiling(menu.Width * scale);
        var height = (int)Math.Ceiling(menu.Height * scale);
        var x = pointer.X - width + (int)(8 * scale);
        var y = pointer.Y - height - (int)(8 * scale);
        if (work is { } area)
        {
            var maxX = area.X + Math.Max(0, area.Width - width);
            var maxY = area.Y + Math.Max(0, area.Height - height);
            x = Math.Clamp(x, area.X, maxX);
            if (y < area.Y) y = Math.Min(pointer.Y + (int)(8 * scale), maxY);
            y = Math.Clamp(y, area.Y, maxY);
        }
        menu.Position = new PixelPoint(x, y);
        menu.Closed += (_, _) => { if (ReferenceEquals(_trayMenuWindow, menu)) _trayMenuWindow = null; };
        _trayMenuWindow = menu;
        menu.Show();
        menu.Activate();
    }

    private void OpenSettingsFromTray()
    {
        ShowMainWindow_OnClick(this, EventArgs.Empty);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { DataContext: MainViewModel main } })
            main.OpenSettingsCommand.Execute(null);
    }

    private void DisposeTrayIcon()
    {
        if (_uptimeTrayIcon is not null)
        {
            _uptimeTrayIcon.MouseUp -= OnTrayMouseUp;
            _uptimeTrayIcon.Visible = false;
            _uptimeTrayIcon.Dispose();
            _uptimeTrayIcon = null;
        }
        _trayImage?.Dispose();
        _trayImage = null;
    }

    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Localizer.Language)) UpdateTrayLanguage();
    }

    private void UpdateTrayLanguage()
    {
        if (_uptimeTrayIcon is not null) _uptimeTrayIcon.Text = Localizer.Instance["TrayTooltip"];
    }
}
