using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace AwayUptime;

public partial class App : System.Windows.Application
{
	private static Mutex? _singleInstanceMutex;

	private NotifyIcon? _notifyIcon;

	private MainWindow? _mainWindow;

	private MiniHudWindow? _miniHud;

	private NightPromptWindow? _nightPrompt;

	private DispatcherTimer? _trayTooltipTimer;

	private DispatcherTimer? _metricsTimer;

	private ContextMenu? _trayContextMenu;

	private System.Windows.Controls.MenuItem? _startupMenuItem;

	private System.Windows.Controls.MenuItem? _showMenuItem;

	private System.Windows.Controls.MenuItem? _copyMenuItem;

	private System.Windows.Controls.MenuItem? _badgeMenuItem;

	private System.Windows.Controls.MenuItem? _hudMenuItem;

	private System.Windows.Controls.MenuItem? _exitMenuItem;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern bool DestroyIcon(nint hIcon);

	[DllImport("user32.dll")]
	private static extern bool SetForegroundWindow(nint hWnd);

	public App()
	{
		ShutdownMode = ShutdownMode.OnExplicitShutdown;
		AppDomain.CurrentDomain.UnhandledException += (object s, UnhandledExceptionEventArgs e) =>
		{
			try
			{
				File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), $"AppDomain Unhandled: {e.ExceptionObject}\n");
			}
			catch
			{
			}
		};
		DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs e) =>
		{
			try
			{
				File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "crash.log"), $"Dispatcher Unhandled: {e.Exception}\n");
			}
			catch
			{
			}
		};
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		try
		{
			_singleInstanceMutex = new Mutex(initiallyOwned: true, "Uptime_SingleInstance_Mutex", out var createdNew);
			if (!createdNew)
			{
				System.Windows.MessageBox.Show("Uptime è già in esecuzione nell'area delle notifiche (System Tray).", "Uptime", System.Windows.MessageBoxButton.OK, MessageBoxImage.Asterisk);
				Shutdown();
				return;
			}
			base.OnStartup(e);
			_mainWindow = new MainWindow();
			_miniHud = new MiniHudWindow();
			_mainWindow.OnToggleHudRequested += ToggleHud;
			_miniHud.OnRequestOpenMainWindow += () =>
			{
				ShowFlyout();
			};
			_miniHud.OnHudStateChanged += () =>
			{
				bool flag = _miniHud?.IsVisible ?? false;
				if (_hudMenuItem != null)
				{
					_hudMenuItem.IsChecked = flag;
				}
				_mainWindow?.SetHudPinned(flag);
			};
			InitializeTrayIcon();
			if (e.Args.Length != 0 && Array.Exists(e.Args, (string a) => a.Equals("--show-hud", StringComparison.OrdinalIgnoreCase)))
			{
				_miniHud.ActivateHud();
			}
			_trayTooltipTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(30.0)
			};
			_trayTooltipTimer.Tick += (object? s, EventArgs ev) =>
			{
				UpdateTrayTooltip();
			};
			_trayTooltipTimer.Start();
			_metricsTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromSeconds(30.0)
			};
			_metricsTimer.Tick += (object? s, EventArgs ev) =>
			{
				SystemMetricsTracker.TickTracker(30);
			};
			_metricsTimer.Start();
			SystemMetricsTracker.OnHealthBreakRecommended += (string duration) =>
			{
				_notifyIcon?.ShowBalloonTip(6000, "Uptime - Promemoria Pausa", "Sei attivo da oltre 3 ore consecutive! Fai una breve pausa per riposare gli occhi.", ToolTipIcon.Info);
			};
			SystemMetricsTracker.OnMilestoneReached += (string milestone) =>
			{
				_notifyIcon?.ShowBalloonTip(7000, "Uptime - Traguardo Raggiunto! \ud83c\udfc6", "Congratulazioni! Il tuo PC ha raggiunto il traguardo di " + milestone + " di stabilità ininterrotta!", ToolTipIcon.Info);
			};
			SystemMetricsTracker.OnNightIdleDetected += () =>
			{
				Dispatcher.Invoke(() =>
				{
					if (_nightPrompt == null || !_nightPrompt.IsLoaded)
					{
						_nightPrompt = new NightPromptWindow();
					}
					_nightPrompt.ShowPrompt();
				});
			};
			SystemMetricsTracker.OnMaintenanceRecommended += (int days) =>
			{
				LanguageProfile current = LocalizationEngine.Current;
				_notifyIcon?.ShowBalloonTip(8000, current.MaintenanceNotificationTitle, string.Format(current.MaintenanceNotificationMsg, days), ToolTipIcon.Warning);
			};
			UptimeHelper.TrimWorkingSet();
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show("Errore durante l'avvio di Uptime: " + ex.Message, "Uptime Errore", System.Windows.MessageBoxButton.OK, MessageBoxImage.Hand);
			Shutdown();
		}
	}

	private void InitializeTrayIcon()
	{
		_notifyIcon = new NotifyIcon
		{
			Icon = LoadAppIcon(),
			Visible = true,
			Text = SafeTooltip(UptimeHelper.FormatTrayTooltip(UptimeHelper.GetUptime()))
		};
		CreateTrayContextMenu();
		_notifyIcon.MouseClick += (object? s, MouseEventArgs e) =>
		{
			if (e.Button == MouseButtons.Left)
			{
				ShowFlyout();
			}
			else if (e.Button == MouseButtons.Right)
			{
				ShowTrayContextMenu();
			}
		};
		_notifyIcon.BalloonTipClicked += (object? s, EventArgs e) =>
		{
			ShowFlyout();
		};
		LocalizationEngine.OnLanguageChanged += UpdateLocalizedTray;
	}

	private void CreateTrayContextMenu()
	{
		_trayContextMenu = new ContextMenu
		{
			Style = (TryFindResource("FluentTrayContextMenuStyle") as Style)
		};
		Style style = TryFindResource("FluentTrayMenuItemStyle") as Style;
		Style style2 = TryFindResource("FluentTrayMenuItemDangerStyle") as Style;
		Style style3 = TryFindResource("FluentTraySeparatorStyle") as Style;
		LanguageProfile current = LocalizationEngine.Current;
		_showMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = current.ShowMenu,
			FontWeight = FontWeights.SemiBold,
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Timer20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 117, 24)),
				FontSize = 15.0
			}
		};
		_showMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			ShowFlyout();
		};
		_trayContextMenu.Items.Add(_showMenuItem);
		_copyMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = current.CopyMenu,
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.DocumentCopy20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 60)),
				FontSize = 15.0
			}
		};
		_copyMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			TimeSpan uptime = UptimeHelper.GetUptime();
			DateTime bootTime = UptimeHelper.GetBootTime();
			System.Windows.Clipboard.SetText(UptimeHelper.FormatClipboardCopy(uptime, bootTime));
		};
		_trayContextMenu.Items.Add(_copyMenuItem);
		_badgeMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = current.BtnShareBadge,
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Share20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 117, 24)),
				FontSize = 15.0
			}
		};
		_badgeMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			if (BadgeGenerator.CopyBadgeToClipboard(LocalizationEngine.Current))
			{
				_notifyIcon?.ShowBalloonTip(3000, "Uptime", LocalizationEngine.Current.BadgeCopiedToast, ToolTipIcon.Info);
			}
		};
		_trayContextMenu.Items.Add(_badgeMenuItem);
		_trayContextMenu.Items.Add(new Separator
		{
			Style = style3
		});
		_hudMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = "Mini-Widget Desktop (HUD)",
			IsCheckable = true,
			IsChecked = (_miniHud?.IsVisible ?? false),
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Desktop20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 60)),
				FontSize = 15.0
			}
		};
		_hudMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			ToggleHud();
		};
		_trayContextMenu.Items.Add(_hudMenuItem);
		_startupMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = current.StartupOption,
			IsCheckable = true,
			IsChecked = UptimeHelper.IsRunAtStartup(),
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Power20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 160, 60)),
				FontSize = 15.0
			}
		};
		_startupMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			if (_startupMenuItem != null)
			{
				UptimeHelper.SetRunAtStartup(_startupMenuItem.IsChecked);
			}
		};
		_trayContextMenu.Items.Add(_startupMenuItem);
		_trayContextMenu.Items.Add(new Separator
		{
			Style = style3
		});
		_exitMenuItem = new System.Windows.Controls.MenuItem
		{
			Header = current.ExitMenu,
			Style = style2,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Dismiss20,
				Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 90, 90)),
				FontSize = 15.0
			}
		};
		_exitMenuItem.Click += (object s, RoutedEventArgs e) =>
		{
			ShutdownApp();
		};
		_trayContextMenu.Items.Add(_exitMenuItem);
	}

	private void ShowTrayContextMenu()
	{
		if (_trayContextMenu != null)
		{
			if (_startupMenuItem != null)
			{
				_startupMenuItem.IsChecked = UptimeHelper.IsRunAtStartup();
			}
			if (_hudMenuItem != null)
			{
				_hudMenuItem.IsChecked = _miniHud?.IsVisible ?? false;
			}
			if (_mainWindow != null)
			{
				SetForegroundWindow(new WindowInteropHelper(_mainWindow).EnsureHandle());
			}
			_trayContextMenu.Placement = PlacementMode.MousePoint;
			_trayContextMenu.IsOpen = true;
		}
	}

	private void ToggleHud()
	{
		if (_miniHud == null)
		{
			return;
		}
		if (_miniHud.IsVisible)
		{
			_miniHud.DeactivateHud();
			if (_hudMenuItem != null)
			{
				_hudMenuItem.IsChecked = false;
			}
		}
		else
		{
			_miniHud.ActivateHud();
			if (_hudMenuItem != null)
			{
				_hudMenuItem.IsChecked = true;
			}
		}
	}

	private void UpdateLocalizedTray()
	{
		LanguageProfile current = LocalizationEngine.Current;
		if (_showMenuItem != null)
		{
			_showMenuItem.Header = current.ShowMenu;
		}
		if (_copyMenuItem != null)
		{
			_copyMenuItem.Header = current.CopyMenu;
		}
		if (_badgeMenuItem != null)
		{
			_badgeMenuItem.Header = current.BtnShareBadge;
		}
		if (_startupMenuItem != null)
		{
			_startupMenuItem.Header = current.StartupOption;
		}
		if (_exitMenuItem != null)
		{
			_exitMenuItem.Header = current.ExitMenu;
		}
		UpdateTrayTooltip();
	}

	private void ShowFlyout()
	{
		if (_mainWindow == null)
		{
			return;
		}
		if (_mainWindow.IsVisible)
		{
			_mainWindow.Hide();
			return;
		}
		UpdateTrayTooltip();
		if (_startupMenuItem != null)
		{
			_startupMenuItem.IsChecked = UptimeHelper.IsRunAtStartup();
		}
		_mainWindow.SetHudPinned(_miniHud?.IsVisible ?? false);
		_mainWindow.ShowFlyout();
	}

	private void UpdateTrayTooltip()
	{
		if (_notifyIcon != null)
		{
			string text = UptimeHelper.FormatTrayTooltip(UptimeHelper.GetUptime());
			_notifyIcon.Text = SafeTooltip(text);
		}
	}

	private static string SafeTooltip(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return "Uptime";
		}
		if (text.Length <= 63)
		{
			return text;
		}
		return text.Substring(0, 63);
	}

	private static Icon LoadAppIcon()
	{
		try
		{
			string text = Path.Combine(AppContext.BaseDirectory, "app.ico");
			if (File.Exists(text))
			{
				return new Icon(text);
			}
			if (Environment.ProcessPath != null)
			{
				Icon icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath);
				if (icon != null)
				{
					return icon;
				}
			}
		}
		catch
		{
		}
		return CreateClockIcon();
	}

	private static Icon CreateClockIcon()
	{
		using Bitmap bitmap = new Bitmap(32, 32);
		using (Graphics graphics = Graphics.FromImage(bitmap))
		{
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			graphics.Clear(System.Drawing.Color.Transparent);
			using SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(30, 30, 30));
			graphics.FillEllipse(brush, 1, 1, 29, 29);
			using System.Drawing.Pen pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(76, 194, 255), 2.5f);
			graphics.DrawEllipse(pen, 2, 2, 27, 27);
			using System.Drawing.Pen pen2 = new System.Drawing.Pen(System.Drawing.Color.White, 2.2f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			};
			graphics.DrawLine(pen2, 15.5f, 15.5f, 15.5f, 8f);
			graphics.DrawLine(pen2, 15.5f, 15.5f, 21f, 15.5f);
			using SolidBrush brush2 = new SolidBrush(System.Drawing.Color.FromArgb(76, 194, 255));
			graphics.FillEllipse(brush2, 13.5f, 13.5f, 4f, 4f);
		}
		nint hicon = bitmap.GetHicon();
		try
		{
			using Icon icon = Icon.FromHandle(hicon);
			return (Icon)icon.Clone();
		}
		finally
		{
			DestroyIcon(hicon);
		}
	}

	private void ShutdownApp()
	{
		_trayTooltipTimer?.Stop();
		_metricsTimer?.Stop();
		_miniHud?.Close();
		_nightPrompt?.Close();
		if (_mainWindow != null)
		{
			_mainWindow.IsExplicitShutdown = true;
			_mainWindow.Close();
		}
		if (_notifyIcon != null)
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}
		_singleInstanceMutex?.ReleaseMutex();
		Shutdown();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		if (_mainWindow != null)
		{
			_mainWindow.IsExplicitShutdown = true;
			_mainWindow.Close();
		}
		_trayTooltipTimer?.Stop();
		_metricsTimer?.Stop();
		_miniHud?.Close();
		_nightPrompt?.Close();
		if (_notifyIcon != null)
		{
			_notifyIcon.Visible = false;
			_notifyIcon.Dispose();
		}
		base.OnExit(e);
	}
}
