using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Uptime.Core;
using Uptime.Infrastructure;
using Uptime.Presentation.Services;
using Uptime.Presentation.ViewModels;
using Uptime.Presentation.Views;

namespace Uptime.Presentation;

public partial class App : Application
{
    private bool _exitRequested;
    private Win32TrayIcon? _uptimeTrayIcon;
    private TrayMenuWindow? _trayMenuWindow;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        try
        {
            var settingsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "settings.json");
            if (System.IO.File.Exists(settingsPath))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(settingsPath));
                if (doc.RootElement.TryGetProperty("ThemeId", out var elem))
                {
                    var id = elem.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        ThemeManager.ApplyTheme(id);
                }
            }
        }
        catch { }
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
        {
            _uptimeTrayIcon = new Win32TrayIcon(iconStream, Localizer.Instance["TrayTooltip"]);
        }
        _uptimeTrayIcon.LeftClicked += () => Dispatcher.UIThread.Post(() => ShowMainWindow_OnClick(this, EventArgs.Empty));
        _uptimeTrayIcon.RightClicked += (x, y) => Dispatcher.UIThread.Post(() => ShowTrayMenu(new PixelPoint(x, y)));
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        UpdateTrayLanguage();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var wallpaperService = new SupabaseWallpaperService();
            var mainVm = new MainViewModel(
                    new Win32UptimeProvider(),
                    new Win32IdleProvider(),
                    new WindowsEventLogService(),
                    new Win32PowerService(),
                    new Win32StartupService(),
                    new WindowsBatteryService(),
                    new WindowsSleepBlockerService(),
                    new WindowsStartupAppsService(),
                    new WindowsMediaService(),
                    new WindowsKeepAwakeService(),
                    new WindowsPowerPlanService(),
                    new WindowsWorkspaceRestoreService(),
                    new WindowsAppCrashService(),
                    new WindowsMemoryService(),
                    new WindowsNetworkWatchdogService(),
                    wallpaperService);
            var bridgeServer = new Services.LocalWallpaperBridgeServer(wallpaperService, mainVm.Settings);
            bridgeServer.Start();
            desktop.Exit += (_, _) =>
            {
                _exitRequested = true;
                Localizer.Instance.PropertyChanged -= OnLanguageChanged;
                DisposeTrayIcon();
                bridgeServer.Dispose();
                mainVm.Dispose();
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
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
            if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
            window.Activate();
        }
    }

    public void RequestExit()
    {
        _exitRequested = true;
        DisposeTrayIcon();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown(0);
    }

    private void Exit_OnClick(object? sender, EventArgs e) => RequestExit();

    private void ShowTrayMenu(PixelPoint pointer)
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not { DataContext: MainViewModel main } owner) return;

        _trayMenuWindow?.Close();
        var menu = new TrayMenuWindow(main.Monitor,
            () => ShowMainWindow_OnClick(this, EventArgs.Empty),
            OpenSettingsFromTray,
            () => Exit_OnClick(this, EventArgs.Empty),
            () => main.OpenStandByMode(),
            OpenAssistantFromTray);
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

    private void OpenAssistantFromTray()
    {
        ShowMainWindow_OnClick(this, EventArgs.Empty);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { DataContext: MainViewModel main } })
            main.OpenAssistantCommand.Execute(null);
    }

    private void DisposeTrayIcon()
    {
        _uptimeTrayIcon?.Dispose();
        _uptimeTrayIcon = null;
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
