using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace AwayUptime;

public partial class MainWindow : FluentWindow, IComponentConnector
{
	private const int HOTKEY_UPTIME_ID = 9001;
	private const uint MOD_ALT = 1u;
	private const uint MOD_WIN = 8u;
	private const uint MOD_NOREPEAT = 16384u;
	private const uint VK_U = 85u;

	private HwndSource? _hwndSource;

	private readonly DispatcherTimer _timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private readonly DispatcherTimer _copyFeedbackTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.5)
	};

	private readonly DispatcherTimer _diagFeedbackTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.5)
	};

	private readonly DispatcherTimer _shareBadgeFeedbackTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(2.5)
	};

	private string? _pendingAction;
	private bool _isHudPinned;
	private string _selectedTimeFilter = "30d";

	// Widget UI element references for fast in-place update
	private System.Windows.Controls.TextBlock? _txtPrimaryUptime;
	private System.Windows.Controls.TextBlock? _txtSecondaryUptime;
	private System.Windows.Controls.TextBlock? _txtBootTime;
	private System.Windows.Controls.TextBlock? _txtBootDuration;
	private System.Windows.Controls.TextBlock? _txtActiveTimeLabel;
	private System.Windows.Controls.TextBlock? _txtIdleTimeLabel;
	private ColumnDefinition? _colActiveRatio;
	private ColumnDefinition? _colIdleRatio;
	private Border? _brdHealthStatusBadge;
	private SymbolIcon? _iconHealthStatus;
	private System.Windows.Controls.TextBlock? _txtHealthStatusDesc;
	private System.Windows.Controls.TextBlock? _txtWgtReliabilityDesc;
	private System.Windows.Controls.TextBlock? _txtWgtCrashesDesc;
	private System.Windows.Controls.TextBlock? _txtWgtCurrentSessionDesc;
	private StackPanel? _pnlWgtSessionPreview;
	private System.Windows.Controls.TextBlock? _txtWgtStabilityScoreVal;
	private System.Windows.Controls.TextBlock? _txtWgtStabilityScoreDesc;
	private System.Windows.Controls.TextBlock? _txtWgtLongestSessionVal;
	private System.Windows.Controls.TextBlock? _txtWgtObservedSessionsVal;
	private System.Windows.Controls.TextBlock? _txtWgtSystemInfoDesc;

	public bool IsExplicitShutdown { get; set; }
	public event Action? OnToggleHudRequested;

	[DllImport("user32.dll")]
	private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

	[DllImport("user32.dll")]
	private static extern bool UnregisterHotKey(nint hWnd, int id);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetCursorPos(out POINT lpPoint);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}

	private DateTime _suppressDeactivateUntil = DateTime.MinValue;

	protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
	{
		base.OnPreviewKeyDown(e);
		Key key = (e.Key == Key.System) ? e.SystemKey : e.Key;
		if (key == Key.F4 && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
		{
			_suppressDeactivateUntil = DateTime.UtcNow.AddSeconds(5);
		}
		else if (key == Key.PrintScreen || key == Key.Snapshot)
		{
			_suppressDeactivateUntil = DateTime.UtcNow.AddSeconds(5);
		}
	}


	public MainWindow()
	{
		_timer.Tick += (object? s, EventArgs e) => UpdateDisplay();

		_copyFeedbackTimer.Tick += (object? s, EventArgs e) =>
		{
			TxtBtnCopy.Text = LocalizationEngine.Current.CopyButton;
			TxtBtnCopySummary.Text = LocalizationEngine.Current.CopySummaryBtn;
			IconCopySummary.Symbol = SymbolRegular.DocumentCopy16;
			_copyFeedbackTimer.Stop();
		};

		_diagFeedbackTimer.Tick += (object? s, EventArgs e) =>
		{
			IconRefreshDiag.Symbol = SymbolRegular.ArrowClockwise16;
			TxtBtnRefreshDiag.Text = LocalizationEngine.Current.RefreshDiagnosticsBtn;
			_diagFeedbackTimer.Stop();
		};

		_shareBadgeFeedbackTimer.Tick += (object? s, EventArgs e) =>
		{
			TxtBtnShareBadge.Text = LocalizationEngine.Current.BtnShareBadge;
			IconBtnShareBadge.Symbol = SymbolRegular.Share20;
			TxtBtnQuickShare.Text = LocalizationEngine.Current.BtnCardLabel;
			IconBtnQuickShare.Symbol = SymbolRegular.Share16;
			_shareBadgeFeedbackTimer.Stop();
		};

		InitializeComponent();
		LoadWindowIcon();
		ChkStartup.IsChecked = UptimeHelper.IsRunAtStartup();
		LocalizationEngine.OnLanguageChanged += RefreshLocalizedLabels;

		RebuildDashboardUi();
		RefreshLocalizedLabels();
		UpdatePinButtonVisuals();
	}

	private void LoadWindowIcon()
	{
		try
		{
			string icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app.ico");
			if (System.IO.File.Exists(icoPath))
			{
				Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(icoPath, UriKind.Absolute));
			}
			else
			{
				Uri packUri = new Uri("pack://application:,,,/app.ico", UriKind.RelativeOrAbsolute);
				Icon = System.Windows.Media.Imaging.BitmapFrame.Create(packUri);
			}
		}
		catch
		{
		}

		try
		{
			string pngPath = System.IO.Path.Combine(AppContext.BaseDirectory, "app_icon_256.png");
			if (System.IO.File.Exists(pngPath))
			{
				ImgAppLogo.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(pngPath, UriKind.Absolute));
			}
			else
			{
				ImgAppLogo.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/app_icon_256.png", UriKind.RelativeOrAbsolute));
			}
		}
		catch
		{
		}
	}

	private void RefreshLocalizedLabels()
	{
		LanguageProfile current = LocalizationEngine.Current;
		TxtCurrentCode.Text = current.Code;
		TxtCurrentLangName.Text = current.Name;
		TxtTabOverview.Text = current.TabOverview;
		TxtTabRecords.Text = current.TabRecords;
		TxtTabActions.Text = current.TabActions;
		ChkStartup.Content = current.StartupOption;
		TxtBtnCopy.Text = current.CopyButton;
		TxtBtnQuickShare.Text = current.BtnCardLabel;
		BtnQuickShareBadge.ToolTip = current.ToolTipQuickShare;
		if (TxtRecordScoreLabel != null)
		{
			TxtRecordScoreLabel.Text = current.StabilityRateLabel;
		}
		TxtPersonalBestHeader.Text = current.PersonalBestHeader;
		TxtMilestonesHeader.Text = current.MilestonesHeader;
		TxtStabilityHeader.Text = current.StabilityHeader;
		TxtStabilityRateLabel.Text = current.StabilityRateLabel;
		TxtShareCardHeader.Text = current.ShareCardHeader;
		TxtShareCardDesc.Text = current.BadgeSubtext;
		TxtBtnShareBadge.Text = current.BtnShareBadge;
		TxtPowerWarning.Text = current.PowerWarning;
		TxtSafeShutdownTitle.Text = current.SafeShutdownTitle;
		TxtSafeShutdownDesc.Text = current.SafeShutdownDesc;
		TxtBtnSafeShutdown.Text = current.BtnSafeShutdown;
		TxtFastStartupTitle.Text = current.FastStartupTitle;
		TxtHotkeyHint.Text = current.HotkeyHint;
		TxtRebootBiosTitle.Text = current.RebootBiosTitle;
		TxtRebootBiosDesc.Text = current.RebootBiosDesc;
		TxtCleanRebootTitle.Text = current.CleanRebootTitle;
		TxtCleanRebootDesc.Text = current.CleanRebootDesc;
		TxtBtnRebootBios.Text = current.BtnRebootBios;
		TxtBtnCleanReboot.Text = current.BtnCleanReboot;
		TxtTimerTitle.Text = current.TimerTitle;
		BtnCancelTimer.Content = current.CancelTimer;
		BtnModalCancel.Content = current.ConfirmCancel;

		// New localized labels
		TxtCustomizeTitle.Text = current.CustomizeDashboard;
		TxtBtnResetDashboard.Text = current.ResetDefault;
		PresetBtnDefault.Content = current.PresetDefault;
		PresetBtnMinimal.Content = current.PresetMinimal;
		PresetBtnReliability.Content = current.PresetReliability;
		PresetBtnCustom.Content = current.PresetCustom;
		TxtEmptyDashboardNotice.Text = current.EmptyDashboardNotice;

		FilterBtn24h.Content = current.Filter24h;
		FilterBtn7d.Content = current.Filter7d;
		FilterBtn30d.Content = current.Filter30d;
		FilterBtnAll.Content = current.FilterAll;
		TxtSessionTimelineHeader.Text = current.SessionTimelineHeader;
		TxtCleanRateLabel.Text = current.CleanRateLabel;
		TxtAvgDurationLabel.Text = current.AvgDurationLabel;
		TxtObservedSessionsLabel.Text = current.ObservedSessionsLabel;

		TxtBtnExportJson.Text = current.ExportJsonBtn;
		TxtBtnExportCsv.Text = current.ExportCsvBtn;
		TxtRefreshDiagnosticsTitle.Text = current.RefreshDiagnosticsHeader;
		TxtRefreshDiagnosticsDesc.Text = current.RefreshDiagnosticsDesc;
		TxtExportDataHeader.Text = current.ExportDataHeader;
		TxtExportDataDesc.Text = current.ExportDataDesc;
		TxtCopySummaryHeader.Text = current.CopySummaryHeader;
		TxtCopySummaryDesc.Text = current.CopySummaryDesc;
		TxtBtnRefreshDiag.Text = current.RefreshDiagnosticsBtn;
		TxtBtnCopySummary.Text = current.CopySummaryBtn;

		TxtBtnOverviewCustomize.Text = current.BtnEditDashboard;
		BtnCustomizeDashboard.ToolTip = current.ToolTipCustomizeDashboard;
		BtnOverviewCustomize.ToolTip = current.ToolTipCustomizeDashboard;
		BtnQuickShareBadge.ToolTip = current.ToolTipQuickShare;
		BtnCloseWindow.ToolTip = current.ToolTipClose;
		UpdatePinButtonVisuals();

		RebuildDashboardUi();
		UpdateDisplay();
		PopulateMilestones();
		UpdateRecordsView();
	}

	public void ShowFlyout()
	{
		ViewLanguage.Visibility = Visibility.Collapsed;
		ViewDashboardCustomize.Visibility = Visibility.Collapsed;
		ModalConfirmation.Visibility = Visibility.Collapsed;
		TabBtnOverview.IsChecked = true;
		SwitchTab(TabContentOverview);
		UpdateDisplay();
		UpdateRecordsView();
		Show();
		PositionFlyout();
		Activate();
		_timer.Start();
		Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)(() => PositionFlyout()));
	}

	private void PositionFlyout()
	{
		try
		{
			UpdateLayout();
			double num = ActualWidth > 0.0 ? ActualWidth : (Width > 0.0 ? Width : 495.0);
			double num2 = ActualHeight > 0.0 ? ActualHeight : (Height > 0.0 ? Height : 510.0);
			Screen screen = Screen.FromPoint(System.Windows.Forms.Control.MousePosition);
			DpiScale dpi = VisualTreeHelper.GetDpi(this);
			double dpiX = dpi.DpiScaleX > 0.0 ? dpi.DpiScaleX : 1.0;
			double dpiY = dpi.DpiScaleY > 0.0 ? dpi.DpiScaleY : 1.0;
			double leftBound = (double)screen.WorkingArea.Left / dpiX;
			double topBound = (double)screen.WorkingArea.Top / dpiY;
			double rightBound = (double)screen.WorkingArea.Right / dpiX;
			double bottomBound = (double)screen.WorkingArea.Bottom / dpiY;
			double x = rightBound - num - 16.0;
			double y = bottomBound - num2 - 16.0;
			if (x < leftBound + 16.0) x = leftBound + 16.0;
			if (y < topBound + 16.0) y = topBound + 16.0;
			Left = x;
			Top = y;
		}
		catch
		{
			Rect workArea = SystemParameters.WorkArea;
			Left = workArea.Right - (Width > 0.0 ? Width : 495.0) - 16.0;
			Top = workArea.Bottom - (Height > 0.0 ? Height : 510.0) - 16.0;
		}
	}

	// =========================================================================
	// DYNAMIC DASHBOARD REBUILD & RENDERING
	// =========================================================================
	public void RebuildDashboardUi()
	{
		PnlDashboardWidgets.Children.Clear();
		ResetWidgetReferences();

		LanguageProfile current = LocalizationEngine.Current;
		DashboardConfig config = SystemMetricsTracker.GetDashboardConfig();
		var visibleWidgets = config.Widgets.Where(w => w.IsVisible).OrderBy(w => w.Order).ToList();

		if (visibleWidgets.Count == 0)
		{
			BrdEmptyDashboard.Visibility = Visibility.Visible;
			return;
		}

		BrdEmptyDashboard.Visibility = Visibility.Collapsed;

		foreach (var widget in visibleWidgets)
		{
			FrameworkElement? elem = CreateWidgetElement(widget.WidgetId, current);
			if (elem != null)
			{
				PnlDashboardWidgets.Children.Add(elem);
			}
		}

		UpdateDisplay();
	}

	private void ResetWidgetReferences()
	{
		_txtPrimaryUptime = null;
		_txtSecondaryUptime = null;
		_txtBootTime = null;
		_txtBootDuration = null;
		_txtActiveTimeLabel = null;
		_txtIdleTimeLabel = null;
		_colActiveRatio = null;
		_colIdleRatio = null;
		_brdHealthStatusBadge = null;
		_iconHealthStatus = null;
		_txtHealthStatusDesc = null;
		_txtWgtReliabilityDesc = null;
		_txtWgtCrashesDesc = null;
		_txtWgtCurrentSessionDesc = null;
		_pnlWgtSessionPreview = null;
		_txtWgtStabilityScoreVal = null;
		_txtWgtStabilityScoreDesc = null;
		_txtWgtLongestSessionVal = null;
		_txtWgtObservedSessionsVal = null;
		_txtWgtSystemInfoDesc = null;
	}

	private FrameworkElement? CreateWidgetElement(string widgetId, LanguageProfile lang)
	{
		switch (widgetId)
		{
			case "uptime":
			{
				var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
				_txtPrimaryUptime = new System.Windows.Controls.TextBlock
				{
					FontSize = 26,
					FontWeight = FontWeights.Bold,
					Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)),
					TextWrapping = TextWrapping.Wrap
				};
				_txtPrimaryUptime.Effect = new DropShadowEffect
				{
					BlurRadius = 14,
					ShadowDepth = 0,
					Color = Color.FromRgb(255, 117, 24),
					Opacity = 0.45
				};
				_txtSecondaryUptime = new System.Windows.Controls.TextBlock
				{
					FontSize = 13.5,
					FontWeight = FontWeights.SemiBold,
					Foreground = new SolidColorBrush(Color.FromRgb(155, 148, 158)),
					Margin = new Thickness(0, 2, 0, 0)
				};
				sp.Children.Add(_txtPrimaryUptime);
				sp.Children.Add(_txtSecondaryUptime);
				return sp;
			}

			case "boot_time":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				var left = new StackPanel();
				var hdr1 = new System.Windows.Controls.TextBlock
				{
					Text = lang.LastBootHeader,
					FontSize = 10,
					FontWeight = FontWeights.Bold,
					Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152))
				};
				_txtBootTime = new System.Windows.Controls.TextBlock
				{
					FontSize = 12,
					Foreground = new SolidColorBrush(Color.FromRgb(237, 237, 237)),
					Margin = new Thickness(0, 2, 0, 0)
				};
				left.Children.Add(hdr1);
				left.Children.Add(_txtBootTime);
				Grid.SetColumn(left, 0);
				grid.Children.Add(left);

				var right = new StackPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
				var hdr2 = new System.Windows.Controls.TextBlock
				{
					Text = lang.BootDurationHeader,
					FontSize = 10,
					FontWeight = FontWeights.Bold,
					Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)),
					HorizontalAlignment = System.Windows.HorizontalAlignment.Right
				};
				var rightSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 2, 0, 0) };
				rightSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Flash16, Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
				_txtBootDuration = new System.Windows.Controls.TextBlock
				{
					FontSize = 12,
					FontWeight = FontWeights.Bold,
					Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)),
					VerticalAlignment = VerticalAlignment.Center
				};
				rightSp.Children.Add(_txtBootDuration);
				right.Children.Add(hdr2);
				right.Children.Add(rightSp);
				Grid.SetColumn(right, 1);
				grid.Children.Add(right);

				card.Content = grid;
				return card;
			}

			case "active_idle":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();

				var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
				var leftSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Left };
				leftSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Record12, Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
				_txtActiveTimeLabel = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(240, 240, 245)), FontWeight = FontWeights.SemiBold };
				leftSp.Children.Add(_txtActiveTimeLabel);

				var rightSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
				rightSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.WeatherMoon16, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)), Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
				_txtIdleTimeLabel = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 170)) };
				rightSp.Children.Add(_txtIdleTimeLabel);

				topGrid.Children.Add(leftSp);
				topGrid.Children.Add(rightSp);
				sp.Children.Add(topGrid);

				var barGrid = new Grid { Height = 6 };
				_colActiveRatio = new ColumnDefinition { Width = new GridLength(70, GridUnitType.Star) };
				_colIdleRatio = new ColumnDefinition { Width = new GridLength(30, GridUnitType.Star) };
				barGrid.ColumnDefinitions.Add(_colActiveRatio);
				barGrid.ColumnDefinitions.Add(_colIdleRatio);

				var bActive = new Border { Background = new SolidColorBrush(Color.FromRgb(255, 117, 24)), CornerRadius = new CornerRadius(3, 0, 0, 3) };
				bActive.Effect = new DropShadowEffect { BlurRadius = 8, ShadowDepth = 0, Color = Color.FromRgb(255, 117, 24), Opacity = 0.5 };
				var bIdle = new Border { Background = new SolidColorBrush(Color.FromRgb(36, 35, 46)), CornerRadius = new CornerRadius(0, 3, 3, 0) };
				Grid.SetColumn(bActive, 0);
				Grid.SetColumn(bIdle, 1);
				barGrid.Children.Add(bActive);
				barGrid.Children.Add(bIdle);
				sp.Children.Add(barGrid);

				card.Content = sp;
				return card;
			}

			case "health_advisor":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				_brdHealthStatusBadge = new Border
				{
					Width = 36,
					Height = 36,
					CornerRadius = new CornerRadius(18),
					Background = new SolidColorBrush(Color.FromRgb(40, 28, 16)),
					BorderBrush = new SolidColorBrush(Color.FromRgb(200, 110, 30)),
					BorderThickness = new Thickness(1),
					Margin = new Thickness(0, 0, 10, 0),
					VerticalAlignment = VerticalAlignment.Center
				};
				_iconHealthStatus = new SymbolIcon
				{
					Symbol = SymbolRegular.ShieldCheckmark24,
					FontSize = 18,
					Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 48)),
					HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				_brdHealthStatusBadge.Child = _iconHealthStatus;
				Grid.SetColumn(_brdHealthStatusBadge, 0);
				grid.Children.Add(_brdHealthStatusBadge);

				var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Shield20, Foreground = new SolidColorBrush(Color.FromRgb(180, 160, 145)), Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.HealthAdvisorHeader, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(190, 175, 160)), VerticalAlignment = VerticalAlignment.Center });
				_txtHealthStatusDesc = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(225, 225, 230)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };

				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtHealthStatusDesc);
				Grid.SetColumn(sp, 1);
				grid.Children.Add(sp);

				card.Content = grid;
				return card;
			}

			case "reliability_summary":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.ShieldTask16, Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetReliabilitySummary, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtReliabilityDesc = new System.Windows.Controls.TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 240)), TextWrapping = TextWrapping.Wrap };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtReliabilityDesc);
				card.Content = sp;
				return card;
			}

			case "crashes_summary":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Warning16, Foreground = new SolidColorBrush(Color.FromRgb(244, 208, 111)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetCrashesSummary, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtCrashesDesc = new System.Windows.Controls.TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 240)), TextWrapping = TextWrapping.Wrap };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtCrashesDesc);
				card.Content = sp;
				return card;
			}

			case "current_session":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.PlayCircle16, Foreground = new SolidColorBrush(Color.FromRgb(82, 209, 124)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetCurrentSession, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtCurrentSessionDesc = new System.Windows.Controls.TextBlock { FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 240)), TextWrapping = TextWrapping.Wrap };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtCurrentSessionDesc);
				card.Content = sp;
				return card;
			}

			case "session_history_preview":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.History16, Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetSessionPreview, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_pnlWgtSessionPreview = new StackPanel();
				sp.Children.Add(hdrSp);
				sp.Children.Add(_pnlWgtSessionPreview);
				card.Content = sp;
				return card;
			}

			case "stability_score":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
				left.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetStabilityScore, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)), Margin = new Thickness(0, 0, 0, 2) });
				_txtWgtStabilityScoreDesc = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)), TextWrapping = TextWrapping.Wrap };
				left.Children.Add(_txtWgtStabilityScoreDesc);
				Grid.SetColumn(left, 0);
				grid.Children.Add(left);

				_txtWgtStabilityScoreVal = new System.Windows.Controls.TextBlock
				{
					FontSize = 22,
					FontWeight = FontWeights.Bold,
					Foreground = new SolidColorBrush(Color.FromRgb(82, 209, 124)),
					VerticalAlignment = VerticalAlignment.Center,
					HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
					Margin = new Thickness(8, 0, 0, 0)
				};
				Grid.SetColumn(_txtWgtStabilityScoreVal, 1);
				grid.Children.Add(_txtWgtStabilityScoreVal);

				card.Content = grid;
				return card;
			}

			case "longest_session":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Trophy16, Foreground = new SolidColorBrush(Color.FromRgb(244, 208, 111)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetLongestSession, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtLongestSessionVal = new System.Windows.Controls.TextBlock { FontSize = 14, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(244, 208, 111)) };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtLongestSessionVal);
				card.Content = sp;
				return card;
			}

			case "observed_sessions":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.DataHistogram16, Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetObservedSessions, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtObservedSessionsVal = new System.Windows.Controls.TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 240)) };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtObservedSessionsVal);
				card.Content = sp;
				return card;
			}

			case "system_info":
			{
				var card = new Card { Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 8, 12, 8) };
				var sp = new StackPanel();
				var hdrSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
				hdrSp.Children.Add(new SymbolIcon { Symbol = SymbolRegular.Desktop16, Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255)), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
				hdrSp.Children.Add(new System.Windows.Controls.TextBlock { Text = lang.WidgetSystemInfo, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 152)) });
				_txtWgtSystemInfoDesc = new System.Windows.Controls.TextBlock { FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(190, 190, 205)), TextWrapping = TextWrapping.Wrap };
				sp.Children.Add(hdrSp);
				sp.Children.Add(_txtWgtSystemInfoDesc);
				card.Content = sp;
				return card;
			}

			default:
				return null;
		}
	}

	// =========================================================================
	// LIVE DATA DISPLAY UPDATE
	// =========================================================================
	private void UpdateDisplay()
	{
		TimeSpan uptime = UptimeHelper.GetUptime();
		DateTime bootTime = UptimeHelper.GetBootTime();
		LanguageProfile current = LocalizationEngine.Current;
		var (pri, sec) = UptimeHelper.FormatDisplay(uptime);

		if (_txtPrimaryUptime != null) _txtPrimaryUptime.Text = pri;
		if (_txtSecondaryUptime != null) _txtSecondaryUptime.Text = sec;

		if (_txtBootTime != null)
		{
			_txtBootTime.Text = bootTime.ToString("dddd d MMMM yyyy, HH:mm:ss", current.Culture);
		}
		if (_txtBootDuration != null)
		{
			_txtBootDuration.Text = SystemMetricsTracker.GetBootDurationString();
		}

		var (timeSpan, timeSpan2, activePct) = SystemMetricsTracker.GetActiveVsIdle();
		if (_colActiveRatio != null && _colIdleRatio != null)
		{
			_colActiveRatio.Width = new GridLength(Math.Max(1.0, activePct), GridUnitType.Star);
			_colIdleRatio.Width = new GridLength(Math.Max(1.0, 100.0 - activePct), GridUnitType.Star);
		}
		if (_txtActiveTimeLabel != null)
		{
			_txtActiveTimeLabel.Text = $"{current.ActiveLabel}: {(int)timeSpan.TotalHours}h {timeSpan.Minutes:D2}m ({activePct:F0}%)";
		}
		if (_txtIdleTimeLabel != null)
		{
			_txtIdleTimeLabel.Text = $"{current.IdleLabel}: {(int)timeSpan2.TotalHours}h {timeSpan2.Minutes:D2}m ({100.0 - activePct:F0}%)";
		}

		// Personal Best
		var (pbUptime, pbDate) = SystemMetricsTracker.GetPersonalBest();
		TxtRecordUptime.Text = UptimeHelper.FormatDisplay(pbUptime).primary;
		TxtRecordDate.Text = pbDate.ToString("d MMMM yyyy, HH:mm", current.Culture);
		if (_txtWgtLongestSessionVal != null)
		{
			_txtWgtLongestSessionVal.Text = $"{UptimeHelper.FormatDisplay(pbUptime).primary} ({pbDate.ToString("d MMM yyyy", current.Culture)})";
		}

		// Health Advisor
		int days = (int)uptime.TotalDays;
		if (_brdHealthStatusBadge != null && _iconHealthStatus != null && _txtHealthStatusDesc != null)
		{
			if (days < 3)
			{
				_iconHealthStatus.Symbol = SymbolRegular.ShieldCheckmark24;
				_iconHealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 48));
				_brdHealthStatusBadge.Background = new SolidColorBrush(Color.FromRgb(40, 28, 16));
				_brdHealthStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(200, 110, 30));
				_txtHealthStatusDesc.Text = current.HealthStatusGreen;
			}
			else if (days < 7)
			{
				_iconHealthStatus.Symbol = SymbolRegular.Warning24;
				_iconHealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 180, 50));
				_brdHealthStatusBadge.Background = new SolidColorBrush(Color.FromRgb(50, 34, 18));
				_brdHealthStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 130, 40));
				_txtHealthStatusDesc.Text = current.HealthStatusYellow;
			}
			else
			{
				_iconHealthStatus.Symbol = SymbolRegular.ArrowClockwise24;
				_iconHealthStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 90, 60));
				_brdHealthStatusBadge.Background = new SolidColorBrush(Color.FromRgb(55, 22, 22));
				_brdHealthStatusBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(170, 50, 40));
				_txtHealthStatusDesc.Text = current.HealthStatusOrange;
			}
		}

		BtnCancelTimer.Visibility = SystemMetricsTracker.IsShutdownTimerActive ? Visibility.Visible : Visibility.Collapsed;
		UpdateFastStartupVisuals();
		UpdateCrashReportVisuals();

		// System info widget
		if (_txtWgtSystemInfoDesc != null)
		{
			var (totalRam, availRam, loadPct) = UptimeHelper.GetMemoryDisplayInfo();
			string os = UptimeHelper.GetOsVersionString();
			string cpu = UptimeHelper.GetProcessorName();
			_txtWgtSystemInfoDesc.Text = $"{os}\n{cpu}\nRAM: {totalRam} ({loadPct}% in use, {availRam} available)";
		}

		// Current session widget
		if (_txtWgtCurrentSessionDesc != null)
		{
			_txtWgtCurrentSessionDesc.Text = $"{current.StartedLabel}: {bootTime:HH:mm:ss} • {current.TimelineDurationLabel}: {(int)uptime.TotalHours}h {uptime.Minutes}m {uptime.Seconds}s";
		}

		// Reliability widgets
		CrashReport crashReport = SystemMetricsTracker.GetCrashReport();
		if (_txtWgtReliabilityDesc != null)
		{
			_txtWgtReliabilityDesc.Text = $"{crashReport.CleanShutdownsLast30Days} clean shutdowns • {crashReport.UnexpectedShutdownsLast30Days} crashes detected (last 30 days)";
		}
		if (_txtWgtCrashesDesc != null)
		{
			_txtWgtCrashesDesc.Text = crashReport.UnexpectedShutdownsLast30Days == 0 ? current.NoCrashesMessage : string.Format(current.CrashesDetectedFormat, crashReport.UnexpectedShutdownsLast30Days);
		}
		if (_txtWgtStabilityScoreVal != null)
		{
			if (crashReport.StabilityScorePercent < 0.0 || crashReport.ConfidenceLevel == "Unavailable" || crashReport.ConfidenceLevel == "Insufficient Data")
			{
				_txtWgtStabilityScoreVal.Text = "N/A";
				_txtWgtStabilityScoreVal.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 175));
			}
			else
			{
				_txtWgtStabilityScoreVal.Text = $"{crashReport.StabilityScorePercent:F1}%";
				_txtWgtStabilityScoreVal.Foreground = crashReport.StabilityScorePercent >= 98.0
					? new SolidColorBrush(Color.FromRgb(82, 209, 124))
					: (crashReport.StabilityScorePercent >= 90.0
						? new SolidColorBrush(Color.FromRgb(244, 208, 111))
						: new SolidColorBrush(Color.FromRgb(255, 120, 120)));
			}
		}
		if (_txtWgtStabilityScoreDesc != null)
		{
			_txtWgtStabilityScoreDesc.Text = crashReport.ScoreExplanation;
		}

		var allSessions = SystemMetricsTracker.GetRecentSessions();
		if (_txtWgtObservedSessionsVal != null)
		{
			_txtWgtObservedSessionsVal.Text = $"{allSessions.Count} sessioni registrate • {crashReport.CleanSessionRatePercent:F0}% tasso pulito";
		}

		if (_pnlWgtSessionPreview != null)
		{
			_pnlWgtSessionPreview.Children.Clear();
			foreach (var s in allSessions.OrderByDescending(x => x.BootTime).Take(2))
			{
				string status = s.IsCurrentSession ? "Attiva" : (s.TerminationType == "UnexpectedShutdown" ? "Arresto Anomalo" : "Terminata");
				var tb = new System.Windows.Controls.TextBlock
				{
					Text = $"• {s.BootTime:dd/MM HH:mm} ({FormatDuration(s.DurationSeconds)}) — {status}",
					FontSize = 10.5,
					Foreground = new SolidColorBrush(s.TerminationType == "UnexpectedShutdown" ? Color.FromRgb(255, 120, 120) : Color.FromRgb(180, 185, 195)),
					Margin = new Thickness(0, 1, 0, 1)
				};
				_pnlWgtSessionPreview.Children.Add(tb);
			}
		}
	}

	private void UpdateFastStartupVisuals()
	{
		LanguageProfile current = LocalizationEngine.Current;
		if (SystemMetricsTracker.IsFastStartupEnabled())
		{
			TxtFastStartupStatus.Text = current.FastStartupStatusActive;
			TxtFastStartupStatus.Foreground = new SolidColorBrush(Color.FromRgb(244, 208, 111));
			BrdFastStartupStatus.Background = new SolidColorBrush(Color.FromRgb(46, 36, 16));
			BrdFastStartupStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(138, 107, 26));
			TxtFastStartupDesc.Text = current.FastStartupDescActive;
			TxtBtnToggleFastStartup.Text = current.BtnDisableFastStartup;
			IconBtnToggleFastStartup.Symbol = SymbolRegular.Power20;
		}
		else
		{
			TxtFastStartupStatus.Text = current.FastStartupStatusDisabled;
			TxtFastStartupStatus.Foreground = new SolidColorBrush(Color.FromRgb(82, 209, 124));
			BrdFastStartupStatus.Background = new SolidColorBrush(Color.FromRgb(22, 45, 34));
			BrdFastStartupStatus.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 106, 79));
			TxtFastStartupDesc.Text = current.FastStartupDescDisabled;
			TxtBtnToggleFastStartup.Text = current.BtnEnableFastStartup;
			IconBtnToggleFastStartup.Symbol = SymbolRegular.Checkmark20;
		}
	}

	private void UpdateCrashReportVisuals()
	{
		LanguageProfile current = LocalizationEngine.Current;
		CrashReport crashReport = SystemMetricsTracker.GetCrashReport();
		string scoreText = crashReport.StabilityScorePercent >= 0.0 ? $"{crashReport.StabilityScorePercent:F1}%" : "N/A";
		SolidColorBrush scoreColor = crashReport.StabilityScorePercent >= 98.0
			? new SolidColorBrush(Color.FromRgb(82, 209, 124))
			: (crashReport.StabilityScorePercent >= 90.0
				? new SolidColorBrush(Color.FromRgb(244, 208, 111))
				: new SolidColorBrush(Color.FromRgb(255, 120, 120)));

		TxtStabilityScore.Text = scoreText;
		TxtStabilityScore.Foreground = scoreColor;

		if (TxtRecordScore != null)
		{
			TxtRecordScore.Text = scoreText;
			TxtRecordScore.Foreground = scoreColor;
		}

		TxtCrashSummary.Text = crashReport.UnexpectedShutdownsLast30Days == 0 ? current.NoCrashesMessage : string.Format(current.CrashesDetectedFormat, crashReport.UnexpectedShutdownsLast30Days);
		TxtLastCrashDate.Text = crashReport.LastCrashDate.HasValue
			? string.Format(current.LastCrashFormat, crashReport.LastCrashDate.Value.ToString("dd/MM/yyyy HH:mm", current.Culture))
			: current.NoRecentCrashMessage;
	}

	private void PopulateMilestones()
	{
		PnlMilestones.Children.Clear();
		int days = (int)UptimeHelper.GetUptime().TotalDays;
		int[] milestones = new int[] { 1, 3, 7, 14, 30, 60, 100 };

		foreach (int m in milestones)
		{
			bool reached = days >= m;
			var border = new Border
			{
				Background = new SolidColorBrush(reached ? Color.FromArgb(255, 24, 48, 68) : Color.FromArgb(255, 26, 26, 30)),
				BorderBrush = new SolidColorBrush(reached ? Color.FromArgb(255, 76, 194, 255) : Color.FromArgb(255, 45, 45, 52)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(8, 4, 8, 4),
				Margin = new Thickness(0, 0, 6, 6)
			};
			var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
			sp.Children.Add(new SymbolIcon
			{
				Symbol = reached ? SymbolRegular.Checkmark12 : SymbolRegular.LockClosed12,
				FontSize = 10,
				Foreground = new SolidColorBrush(reached ? Color.FromArgb(255, 76, 194, 255) : Color.FromArgb(255, 120, 120, 130)),
				Margin = new Thickness(0, 0, 4, 0),
				VerticalAlignment = VerticalAlignment.Center
			});
			sp.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = m == 1 ? "24h" : $"{m}d",
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(reached ? Color.FromArgb(255, 235, 235, 245) : Color.FromArgb(255, 130, 130, 140)),
				VerticalAlignment = VerticalAlignment.Center
			});
			border.Child = sp;
			PnlMilestones.Children.Add(border);
		}
	}

	// =========================================================================
	// RECORDS TAB & SESSION TIMELINE
	// =========================================================================
	private void FilterBtn_Click(object sender, RoutedEventArgs e)
	{
		if (FilterBtn24h.IsChecked == true) _selectedTimeFilter = "24h";
		else if (FilterBtn7d.IsChecked == true) _selectedTimeFilter = "7d";
		else if (FilterBtn30d.IsChecked == true) _selectedTimeFilter = "30d";
		else if (FilterBtnAll.IsChecked == true) _selectedTimeFilter = "all";

		UpdateRecordsView();
	}

	private void UpdateRecordsView()
	{
		DateTime cutoff = ExportHelper.GetFilterCutoff(_selectedTimeFilter);
		var allSessions = SystemMetricsTracker.GetRecentSessions();
		var filtered = allSessions.Where(s => s.BootTime >= cutoff).OrderByDescending(s => s.BootTime).ToList();

		TxtFilteredSessionsCount.Text = filtered.Count.ToString();

		if (filtered.Count > 0)
		{
			double maxSecs = filtered.Max(s => s.DurationSeconds);
			double avgSecs = filtered.Average(s => s.DurationSeconds);
			int cleanCount = filtered.Count(s => s.TerminationType == "NormalShutdown" || s.IsCurrentSession);
			double cleanPct = (double)cleanCount / filtered.Count * 100.0;

			TxtFilteredLongestSession.Text = FormatDuration(maxSecs);
			TxtFilteredAvgDuration.Text = FormatDuration(avgSecs);
			TxtFilteredCleanRate.Text = $"{cleanPct:F0}%";
		}
		else
		{
			TxtFilteredLongestSession.Text = "0h";
			TxtFilteredAvgDuration.Text = "0h";
			TxtFilteredCleanRate.Text = "100%";
		}

		// Populate timeline items
		PnlSessionTimeline.Children.Clear();
		LanguageProfile current = LocalizationEngine.Current;

		if (filtered.Count == 0)
		{
			PnlSessionTimeline.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = current.TimelineNoSessionsNotice,
				FontSize = 11,
				Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
				Margin = new Thickness(0, 4, 0, 4)
			});
			return;
		}

		foreach (var s in filtered.Take(15)) // Top 15 in UI for optimal performance
		{
			var rowBorder = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(160, 22, 20, 28)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(160, 52, 42, 56)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10, 6, 10, 6),
				Margin = new Thickness(0, 0, 0, 5)
			};

			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var left = new StackPanel();
			var titleSp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
			titleSp.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = s.BootTime.ToString("dd MMM yyyy, HH:mm", current.Culture),
				FontSize = 11,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 240))
			});

			if (s.IsCurrentSession)
			{
				var curBadge = new Border
				{
					Background = new SolidColorBrush(Color.FromRgb(24, 48, 72)),
					BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, 255)),
					BorderThickness = new Thickness(1),
					CornerRadius = new CornerRadius(3),
					Padding = new Thickness(4, 1, 4, 1),
					Margin = new Thickness(6, 0, 0, 0),
					VerticalAlignment = VerticalAlignment.Center
				};
				curBadge.Child = new System.Windows.Controls.TextBlock { Text = current.TimelineActiveSessionBadge, FontSize = 8.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255)) };
				titleSp.Children.Add(curBadge);
			}

			left.Children.Add(titleSp);

			string detailText = $"{current.TimelineDurationLabel}: {FormatDuration(s.DurationSeconds)}";
			if (!string.IsNullOrEmpty(s.EvidenceSummary))
			{
				detailText += $" • {FormatEvidenceSummary(s.EvidenceSummary, current)}";
			}

			left.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = detailText,
				FontSize = 10,
				Foreground = new SolidColorBrush(Color.FromRgb(140, 145, 155)),
				Margin = new Thickness(0, 2, 0, 0)
			});

			Grid.SetColumn(left, 0);
			grid.Children.Add(left);

			// Termination badge
			(string termLabel, Color badgeColor, Color badgeBg) = GetTerminationBadgeStyle(s, current);
			var badge = new Border
			{
				Background = new SolidColorBrush(badgeBg),
				BorderBrush = new SolidColorBrush(badgeColor),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(4),
				Padding = new Thickness(6, 2, 6, 2),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = System.Windows.HorizontalAlignment.Right
			};
			badge.Child = new System.Windows.Controls.TextBlock
			{
				Text = termLabel,
				FontSize = 9.5,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(badgeColor)
			};

			Grid.SetColumn(badge, 1);
			grid.Children.Add(badge);

			rowBorder.Child = grid;
			PnlSessionTimeline.Children.Add(rowBorder);
		}
	}

	private static (string label, Color fg, Color bg) GetTerminationBadgeStyle(SessionRecord s, LanguageProfile current)
	{
		if (s.IsCurrentSession)
		{
			return (current.TimelineInProgressBadge, Color.FromRgb(76, 194, 255), Color.FromArgb(60, 24, 48, 72));
		}
		return s.TerminationType switch
		{
			"NormalShutdown" => (current.TimelineNormalShutdownBadge, Color.FromRgb(82, 209, 124), Color.FromArgb(60, 24, 52, 34)),
			"Restart" => (current.TimelineRestartBadge, Color.FromRgb(76, 194, 255), Color.FromArgb(60, 20, 40, 60)),
			"UnexpectedShutdown" => (current.TimelineUnexpectedShutdownBadge, Color.FromRgb(255, 120, 120), Color.FromArgb(70, 60, 20, 24)),
			_ => (current.TimelineCompletedBadge, Color.FromRgb(180, 180, 190), Color.FromArgb(60, 36, 36, 42))
		};
	}

	private static string FormatEvidenceSummary(string evidence, LanguageProfile lang)
	{
		if (string.IsNullOrEmpty(evidence)) return string.Empty;
		if (evidence.Equals("Active Windows boot session.", StringComparison.OrdinalIgnoreCase))
			return lang.TimelineEvidenceActiveBoot;
		if (evidence.Equals("Session completed prior to new boot.", StringComparison.OrdinalIgnoreCase))
			return lang.TimelineEvidenceCompletedPrior;
		return evidence;
	}

	// =========================================================================
	// DASHBOARD CUSTOMIZATION PANEL & PRESETS
	// =========================================================================
	private void BtnCustomizeDashboard_Click(object sender, RoutedEventArgs e)
	{
		PopulateCustomizeWidgetList();
		UpdatePresetButtonState();

		ViewDashboardCustomize.Visibility = Visibility.Visible;
		ViewDashboardCustomize.Opacity = 0.0;
		TransCustomizeView.Y = -35.0;

		var animY = new DoubleAnimation(-35.0, 0.0, TimeSpan.FromMilliseconds(240.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		var animOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		TransCustomizeView.BeginAnimation(TranslateTransform.YProperty, animY);
		ViewDashboardCustomize.BeginAnimation(UIElement.OpacityProperty, animOp);
	}

	private void CloseCustomizePanelAnimated(Action? onComplete = null)
	{
		var animY = new DoubleAnimation(0.0, -30.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		var animOp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		animY.Completed += (s, e) =>
		{
			ViewDashboardCustomize.Visibility = Visibility.Collapsed;
			TransCustomizeView.Y = 0.0;
			ViewDashboardCustomize.Opacity = 1.0;
			onComplete?.Invoke();
		};
		TransCustomizeView.BeginAnimation(TranslateTransform.YProperty, animY);
		ViewDashboardCustomize.BeginAnimation(UIElement.OpacityProperty, animOp);
	}

	private void BtnBackFromCustomize_Click(object sender, RoutedEventArgs e)
	{
		CloseCustomizePanelAnimated();
	}

	private void BtnCloseCustomize_Click(object sender, RoutedEventArgs e)
	{
		CloseCustomizePanelAnimated();
	}

	private void UpdatePresetButtonState()
	{
		var config = SystemMetricsTracker.GetDashboardConfig();
		PresetBtnDefault.IsChecked = config.ActivePreset == DashboardConfig.PresetDefault;
		PresetBtnMinimal.IsChecked = config.ActivePreset == DashboardConfig.PresetMinimal;
		PresetBtnReliability.IsChecked = config.ActivePreset == DashboardConfig.PresetReliability;
		PresetBtnCustom.IsChecked = config.ActivePreset == DashboardConfig.PresetCustom;
	}

	private void PresetBtn_Click(object sender, RoutedEventArgs e)
	{
		var config = SystemMetricsTracker.GetDashboardConfig();
		if (PresetBtnDefault.IsChecked == true) config.ApplyPreset(DashboardConfig.PresetDefault);
		else if (PresetBtnMinimal.IsChecked == true) config.ApplyPreset(DashboardConfig.PresetMinimal);
		else if (PresetBtnReliability.IsChecked == true) config.ApplyPreset(DashboardConfig.PresetReliability);

		SystemMetricsTracker.SaveDashboardConfig(config);
		RebuildDashboardUi();
		PopulateCustomizeWidgetList();
		UpdatePresetButtonState();
	}

	private void BtnResetDashboard_Click(object sender, RoutedEventArgs e)
	{
		var config = SystemMetricsTracker.GetDashboardConfig();
		config.ApplyPreset(DashboardConfig.PresetDefault);
		SystemMetricsTracker.SaveDashboardConfig(config);
		RebuildDashboardUi();
		PopulateCustomizeWidgetList();
		UpdatePresetButtonState();
	}

	private void PopulateCustomizeWidgetList()
	{
		PnlCustomizeWidgetList.Children.Clear();
		LanguageProfile lang = LocalizationEngine.Current;
		DashboardConfig config = SystemMetricsTracker.GetDashboardConfig();
		var sorted = config.Widgets.OrderBy(w => w.Order).ToList();

		for (int i = 0; i < sorted.Count; i++)
		{
			var widget = sorted[i];
			int currentIndex = i;

			var row = new Border
			{
				Background = new SolidColorBrush(widget.IsVisible ? Color.FromArgb(255, 26, 32, 42) : Color.FromArgb(255, 22, 22, 26)),
				BorderBrush = new SolidColorBrush(widget.IsVisible ? Color.FromArgb(255, 45, 65, 85) : Color.FromArgb(255, 38, 38, 44)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10, 7, 8, 7),
				Margin = new Thickness(0, 0, 0, 5)
			};

			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			// Checkbox
			var chk = new System.Windows.Controls.CheckBox
			{
				IsChecked = widget.IsVisible,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 8, 0)
			};
			chk.Click += (s, ev) =>
			{
				config.SetWidgetVisibility(widget.WidgetId, chk.IsChecked == true);
				SystemMetricsTracker.SaveDashboardConfig(config);
				RebuildDashboardUi();
				UpdatePresetButtonState();
				PopulateCustomizeWidgetList();
			};
			Grid.SetColumn(chk, 0);
			grid.Children.Add(chk);

			// Title
			var title = new System.Windows.Controls.TextBlock
			{
				Text = lang.GetWidgetTitle(widget.WidgetId),
				FontSize = 11.5,
				FontWeight = widget.IsVisible ? FontWeights.SemiBold : FontWeights.Normal,
				Foreground = new SolidColorBrush(widget.IsVisible ? Color.FromRgb(235, 235, 245) : Color.FromRgb(140, 140, 150)),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(title, 1);
			grid.Children.Add(title);

			// Move buttons
			var btnPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

			var btnUp = new System.Windows.Controls.Button
			{
				Content = "▲",
				FontSize = 9,
				Width = 24,
				Height = 24,
				Margin = new Thickness(0, 0, 3, 0),
				IsEnabled = currentIndex > 0,
				Background = new SolidColorBrush(Color.FromArgb(120, 40, 45, 55)),
				Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
				BorderThickness = new Thickness(0),
				Cursor = System.Windows.Input.Cursors.Hand
			};
			btnUp.Click += (s, ev) =>
			{
				config.MoveWidget(widget.WidgetId, moveUp: true);
				SystemMetricsTracker.SaveDashboardConfig(config);
				RebuildDashboardUi();
				UpdatePresetButtonState();
				PopulateCustomizeWidgetList();
			};

			var btnDown = new System.Windows.Controls.Button
			{
				Content = "▼",
				FontSize = 9,
				Width = 24,
				Height = 24,
				IsEnabled = currentIndex < sorted.Count - 1,
				Background = new SolidColorBrush(Color.FromArgb(120, 40, 45, 55)),
				Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
				BorderThickness = new Thickness(0),
				Cursor = System.Windows.Input.Cursors.Hand
			};
			btnDown.Click += (s, ev) =>
			{
				config.MoveWidget(widget.WidgetId, moveUp: false);
				SystemMetricsTracker.SaveDashboardConfig(config);
				RebuildDashboardUi();
				UpdatePresetButtonState();
				PopulateCustomizeWidgetList();
			};

			btnPanel.Children.Add(btnUp);
			btnPanel.Children.Add(btnDown);
			Grid.SetColumn(btnPanel, 2);
			grid.Children.Add(btnPanel);

			row.Child = grid;
			PnlCustomizeWidgetList.Children.Add(row);
		}
	}

	// =========================================================================
	// EXPORT & ACTIONS
	// =========================================================================
	private void BtnExportJson_Click(object sender, RoutedEventArgs e)
	{
		var sessions = SystemMetricsTracker.GetRecentSessions();
		var crashReport = SystemMetricsTracker.GetCrashReport();
		if (ExportHelper.PromptAndExportJson(sessions, crashReport, _selectedTimeFilter, out string path))
		{
			System.Windows.MessageBox.Show($"Report salvato con successo in:\n{path}", "Uptime Export", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private void BtnExportCsv_Click(object sender, RoutedEventArgs e)
	{
		var sessions = SystemMetricsTracker.GetRecentSessions();
		if (ExportHelper.PromptAndExportCsv(sessions, _selectedTimeFilter, out string path))
		{
			System.Windows.MessageBox.Show($"File CSV salvato con successo in:\n{path}", "Uptime Export", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
		}
	}

	private async void BtnRefreshDiagnostics_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			IconRefreshDiag.Symbol = SymbolRegular.ArrowClockwise16;
			TxtBtnRefreshDiag.Text = "...";
			await Task.Run(() => SystemMetricsTracker.GetCrashReport(forceRefresh: true));
			LanguageProfile current = LocalizationEngine.Current;
			IconRefreshDiag.Symbol = SymbolRegular.Checkmark16;
			TxtBtnRefreshDiag.Text = current.DiagnosticsRefreshed;
			UpdateCrashReportVisuals();
			UpdateRecordsView();
			_diagFeedbackTimer.Stop();
			_diagFeedbackTimer.Start();
		}
		catch
		{
			IconRefreshDiag.Symbol = SymbolRegular.ArrowClockwise16;
			TxtBtnRefreshDiag.Text = LocalizationEngine.Current.RefreshDiagnosticsBtn;
		}
	}

	private void BtnCopySummary_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			TimeSpan uptime = UptimeHelper.GetUptime();
			DateTime bootTime = UptimeHelper.GetBootTime();
			CrashReport crashReport = SystemMetricsTracker.GetCrashReport();
			var (pbUptime, pbDate) = SystemMetricsTracker.GetPersonalBest();
			LanguageProfile current = LocalizationEngine.Current;

			string summary = $"--- Uptime Reliability Report ---\n" +
				$"Uptime: {current.FormatDisplay(uptime).primary}, {current.FormatDisplay(uptime).secondary}\n" +
				$"Boot: {bootTime:yyyy-MM-dd HH:mm:ss}\n" +
				$"Stability Score: {crashReport.StabilityScorePercent:F1}%\n" +
				$"Observed Clean Shutdowns: {crashReport.CleanShutdownsLast30Days}\n" +
				$"Unexpected Shutdowns: {crashReport.UnexpectedShutdownsLast30Days}\n" +
				$"Personal Best: {current.FormatDisplay(pbUptime).primary} ({pbDate:yyyy-MM-dd})\n" +
				$"OS: {UptimeHelper.GetOsVersionString()}";

			System.Windows.Clipboard.SetText(summary);
			IconCopySummary.Symbol = SymbolRegular.Checkmark16;
			TxtBtnCopySummary.Text = current.CopiedFeedback;
			_copyFeedbackTimer.Stop();
			_copyFeedbackTimer.Start();
		}
		catch
		{
		}
	}

	private void BtnCopy_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			TimeSpan uptime = UptimeHelper.GetUptime();
			DateTime bootTime = UptimeHelper.GetBootTime();
			LanguageProfile current = LocalizationEngine.Current;
			System.Windows.Clipboard.SetText(UptimeHelper.FormatClipboardCopy(uptime, bootTime));
			TxtBtnCopy.Text = current.CopiedFeedback;
			_copyFeedbackTimer.Stop();
			_copyFeedbackTimer.Start();
		}
		catch
		{
		}
	}

	private void BtnShareBadge_Click(object sender, RoutedEventArgs e)
	{
		LanguageProfile current = LocalizationEngine.Current;
		if (BadgeGenerator.CopyBadgeToClipboard(current))
		{
			TxtBtnShareBadge.Text = current.BadgeCopiedToast;
			IconBtnShareBadge.Symbol = SymbolRegular.Checkmark20;
			TxtBtnQuickShare.Text = "✓";
			IconBtnQuickShare.Symbol = SymbolRegular.Checkmark16;
			_shareBadgeFeedbackTimer.Stop();
			_shareBadgeFeedbackTimer.Start();
		}
	}

	// =========================================================================
	// NAVIGATION & TABS
	// =========================================================================
	private void TabBtn_Click(object sender, RoutedEventArgs e)
	{
		if (TabBtnOverview.IsChecked == true)
		{
			SwitchTab(TabContentOverview);
		}
		else if (TabBtnRecords.IsChecked == true)
		{
			UpdateRecordsView();
			SwitchTab(TabContentRecords);
		}
		else if (TabBtnActions.IsChecked == true)
		{
			SwitchTab(TabContentActions);
		}
	}

	private void SwitchTab(Grid activeTab)
	{
		if (activeTab.Visibility != Visibility.Visible || !(activeTab.Opacity > 0.9))
		{
			TabContentOverview.Visibility = Visibility.Collapsed;
			TabContentRecords.Visibility = Visibility.Collapsed;
			TabContentActions.Visibility = Visibility.Collapsed;
			activeTab.Visibility = Visibility.Visible;
			activeTab.Opacity = 0.0;

			TranslateTransform trans = activeTab == TabContentOverview
				? TransTabOverview
				: (activeTab == TabContentRecords ? TransTabRecords : TransTabActions);

			trans.Y = -18.0;
			var animY = new DoubleAnimation(-18.0, 0.0, TimeSpan.FromMilliseconds(220.0))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			var animOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200.0))
			{
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
			};
			trans.BeginAnimation(TranslateTransform.YProperty, animY);
			activeTab.BeginAnimation(UIElement.OpacityProperty, animOp);
		}
	}

	// =========================================================================
	// POWER ACTIONS & MODALS
	// =========================================================================
	private void BtnSafeShutdown_Click(object sender, RoutedEventArgs e)
	{
		LanguageProfile current = LocalizationEngine.Current;
		_pendingAction = "shutdown";
		TxtModalTitle.Text = current.SafeShutdownTitle;
		TxtModalMessage.Text = current.SafeShutdownConfirmMessage;
		BtnModalProceed.Content = current.BtnSafeShutdown;
		ModalConfirmation.Visibility = Visibility.Visible;
	}

	private void BtnRebootBios_Click(object sender, RoutedEventArgs e)
	{
		LanguageProfile current = LocalizationEngine.Current;
		_pendingAction = "bios";
		TxtModalTitle.Text = current.ConfirmTitle;
		TxtModalMessage.Text = current.ConfirmMessage;
		BtnModalProceed.Content = current.ConfirmProceed;
		ModalConfirmation.Visibility = Visibility.Visible;
	}

	private void BtnCleanReboot_Click(object sender, RoutedEventArgs e)
	{
		LanguageProfile current = LocalizationEngine.Current;
		_pendingAction = "clean";
		TxtModalTitle.Text = current.CleanRebootTitle;
		TxtModalMessage.Text = current.CleanRebootConfirmMessage;
		BtnModalProceed.Content = current.BtnCleanReboot;
		ModalConfirmation.Visibility = Visibility.Visible;
	}

	private void BtnModalCancel_Click(object sender, RoutedEventArgs e)
	{
		_pendingAction = null;
		ModalConfirmation.Visibility = Visibility.Collapsed;
	}

	private void BtnModalProceed_Click(object sender, RoutedEventArgs e)
	{
		ModalConfirmation.Visibility = Visibility.Collapsed;
		if (_pendingAction == "shutdown") SystemMetricsTracker.FullSafeShutdown();
		else if (_pendingAction == "bios") SystemMetricsTracker.RebootToBios();
		else if (_pendingAction == "clean") SystemMetricsTracker.CleanReboot();
		_pendingAction = null;
	}

	private void BtnTimer30_Click(object sender, RoutedEventArgs e) => SetTimer(30);
	private void BtnTimer60_Click(object sender, RoutedEventArgs e) => SetTimer(60);
	private void BtnTimer120_Click(object sender, RoutedEventArgs e) => SetTimer(120);

	private void SetTimer(int minutes)
	{
		SystemMetricsTracker.StartShutdownTimer(minutes);
		BtnCancelTimer.Visibility = Visibility.Visible;
		System.Windows.MessageBox.Show($"PC programmato per lo spegnimento tra {minutes} minuti.", "Uptime Timer", System.Windows.MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private void BtnCancelTimer_Click(object sender, RoutedEventArgs e)
	{
		SystemMetricsTracker.CancelShutdownTimer();
		BtnCancelTimer.Visibility = Visibility.Collapsed;
		System.Windows.MessageBox.Show("Timer di spegnimento annullato con successo.", "Uptime Timer", System.Windows.MessageBoxButton.OK, MessageBoxImage.Asterisk);
	}

	private async void BtnToggleFastStartup_Click(object sender, RoutedEventArgs e)
	{
		bool flag = SystemMetricsTracker.IsFastStartupEnabled();
		bool newState = !flag;
		TxtBtnToggleFastStartup.Text = "...";
		await Task.Run(() => SystemMetricsTracker.SetFastStartup(newState));
		UpdateFastStartupVisuals();
	}

	private void ChkStartup_Click(object sender, RoutedEventArgs e)
	{
		UptimeHelper.SetRunAtStartup(ChkStartup.IsChecked == true);
	}

	// =========================================================================
	// PIN HUD & WINDOW MANAGEMENT
	// =========================================================================
	public void SetHudPinned(bool isPinned)
	{
		_isHudPinned = isPinned;
		UpdatePinButtonVisuals();
	}

	private void UpdatePinButtonVisuals()
	{
		if (BtnPinHud != null && IconPinHud != null)
		{
			if (_isHudPinned)
			{
				IconPinHud.Symbol = SymbolRegular.Pin20;
				IconPinHud.Foreground = new SolidColorBrush(Color.FromRgb(76, 194, 255));
				BtnPinHud.Background = new SolidColorBrush(Color.FromRgb(22, 48, 72));
				BtnPinHud.BorderBrush = new SolidColorBrush(Color.FromRgb(76, 194, 255));
				BrdPinDot.Visibility = Visibility.Visible;
				BtnPinHud.ToolTip = LocalizationEngine.Current.ToolTipUnpinHud;
			}
			else
			{
				IconPinHud.Symbol = SymbolRegular.Pin20;
				IconPinHud.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 175));
				BtnPinHud.Background = Brushes.Transparent;
				BtnPinHud.BorderBrush = Brushes.Transparent;
				BrdPinDot.Visibility = Visibility.Collapsed;
				BtnPinHud.ToolTip = LocalizationEngine.Current.ToolTipPinHud;
			}
		}
	}

	private void BtnPinHud_Click(object sender, RoutedEventArgs e)
	{
		var anim1 = new DoubleAnimation(1.0, 0.82, TimeSpan.FromMilliseconds(75.0))
		{
			AutoReverse = true,
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		var anim2 = new DoubleAnimation(1.0, 0.82, TimeSpan.FromMilliseconds(75.0))
		{
			AutoReverse = true,
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		TransScalePin.BeginAnimation(ScaleTransform.ScaleXProperty, anim1);
		TransScalePin.BeginAnimation(ScaleTransform.ScaleYProperty, anim2);
		OnToggleHudRequested?.Invoke();
	}

	private void Window_Deactivated(object sender, EventArgs e)
	{
		if (ModalConfirmation.Visibility == Visibility.Visible || ViewDashboardCustomize.Visibility == Visibility.Visible)
		{
			return;
		}

		// 1. If F4 / PrintScreen was pressed recently, suppress hiding
		if (DateTime.UtcNow < _suppressDeactivateUntil)
		{
			return;
		}

		// 2. Check if the newly activated/foreground window belongs to a screen capture tool (ShareX, Snipping Tool, etc.)
		try
		{
			IntPtr fgHwnd = GetForegroundWindow();
			if (fgHwnd != IntPtr.Zero)
			{
				GetWindowThreadProcessId(fgHwnd, out uint pid);
				if (pid != 0)
				{
					using var proc = Process.GetProcessById((int)pid);
					string name = proc.ProcessName.ToLowerInvariant();
					if (name.Contains("sharex") ||
					    name.Contains("snipping") ||
					    name.Contains("screenclipping") ||
					    name.Contains("lightshot") ||
					    name.Contains("greenshot") ||
					    name.Contains("flameshot") ||
					    name.Contains("screenrec") ||
					    name.Contains("obs") ||
					    name.Contains("capture"))
					{
						return; // Screen capture tool is active! Keep Uptime visible.
					}
				}
			}
		}
		catch
		{
		}

		// 3. Check if mouse cursor is currently within the Uptime window rect (e.g. user region-selecting over Uptime)
		try
		{
			if (GetCursorPos(out POINT pt))
			{
				var rect = new Rect(Left, Top, Width, Height);
				rect.Inflate(12, 12);
				if (rect.Contains(new System.Windows.Point(pt.X, pt.Y)))
				{
					IntPtr fg = GetForegroundWindow();
					if (fg != IntPtr.Zero)
					{
						GetWindowThreadProcessId(fg, out uint pId);
						if (pId != 0)
						{
							using var p = Process.GetProcessById((int)pId);
							string pName = p.ProcessName.ToLowerInvariant();
							if (pName == "explorer" || pName.Contains("overlay") || pName.Contains("sharex"))
							{
								return;
							}
						}
					}
				}
			}
		}
		catch
		{
		}

		HideWindow();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		HideWindow();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (!IsExplicitShutdown)
		{
			e.Cancel = true;
			HideWindow();
		}
	}

	private void HideWindow()
	{
		_timer?.Stop();
		Hide();
		UptimeHelper.TrimWorkingSet();
	}

	public void ToggleFlyout()
	{
		if (Visibility == Visibility.Visible && IsActive) HideWindow();
		else ShowFlyout();
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		try
		{
			var helper = new WindowInteropHelper(this);
			_hwndSource = HwndSource.FromHwnd(helper.Handle);
			_hwndSource?.AddHook(HwndHook);
			RegisterHotKey(helper.Handle, HOTKEY_UPTIME_ID, MOD_NOREPEAT | MOD_WIN | MOD_ALT, VK_U);
		}
		catch
		{
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		try
		{
			UnregisterHotKey(new WindowInteropHelper(this).Handle, HOTKEY_UPTIME_ID);
			_hwndSource?.RemoveHook(HwndHook);
		}
		catch
		{
		}
		base.OnClosed(e);
	}

	private nint HwndHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
	{
		if (msg == 786 && ((IntPtr)wParam).ToInt32() == HOTKEY_UPTIME_ID)
		{
			handled = true;
			ToggleFlyout();
		}
		return IntPtr.Zero;
	}

	// =========================================================================
	// LANGUAGE PICKER VIEW
	// =========================================================================
	private void BtnLangSelect_Click(object sender, RoutedEventArgs e)
	{
		PopulateLanguageList();
		ViewLanguage.Visibility = Visibility.Visible;
		ViewLanguage.Opacity = 0.0;
		TransLangView.Y = -35.0;

		var animY = new DoubleAnimation(-35.0, 0.0, TimeSpan.FromMilliseconds(240.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		var animOp = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
		};
		TransLangView.BeginAnimation(TranslateTransform.YProperty, animY);
		ViewLanguage.BeginAnimation(UIElement.OpacityProperty, animOp);
	}

	private void CloseLanguagePickerAnimated(Action? onComplete = null)
	{
		var animY = new DoubleAnimation(0.0, -30.0, TimeSpan.FromMilliseconds(180.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		var animOp = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160.0))
		{
			EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
		};
		animY.Completed += (s, e) =>
		{
			ViewLanguage.Visibility = Visibility.Collapsed;
			TransLangView.Y = 0.0;
			ViewLanguage.Opacity = 1.0;
			onComplete?.Invoke();
		};
		TransLangView.BeginAnimation(TranslateTransform.YProperty, animY);
		ViewLanguage.BeginAnimation(UIElement.OpacityProperty, animOp);
	}

	private void BtnBackFromLang_Click(object sender, RoutedEventArgs e)
	{
		CloseLanguagePickerAnimated();
	}

	private void PopulateLanguageList()
	{
		PnlLanguageList.Children.Clear();
		LanguageProfile current = LocalizationEngine.Current;

		foreach (LanguageProfile lang in LocalizationEngine.SupportedLanguages)
		{
			bool isSelected = lang.Id == current.Id;
			var border = new Border
			{
				Background = new SolidColorBrush(isSelected ? Color.FromArgb(255, 42, 26, 20) : Color.FromArgb(255, 25, 23, 32)),
				BorderBrush = new SolidColorBrush(isSelected ? Color.FromArgb(255, 255, 117, 24) : Color.FromArgb(255, 54, 44, 62)),
				BorderThickness = new Thickness(isSelected ? 1.5 : 1),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(12, 9, 12, 9),
				Margin = new Thickness(0, 0, 0, 6),
				Cursor = System.Windows.Input.Cursors.Hand
			};

			var grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var flagBorder = new Border
			{
				Background = new SolidColorBrush(isSelected ? Color.FromArgb(255, 60, 32, 18) : Color.FromArgb(255, 36, 28, 36)),
				BorderBrush = new SolidColorBrush(isSelected ? Color.FromArgb(255, 255, 117, 24) : Color.FromArgb(255, 78, 54, 48)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(4),
				Padding = new Thickness(6, 2, 6, 2),
				Margin = new Thickness(0, 0, 12, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			flagBorder.Child = new System.Windows.Controls.TextBlock
			{
				Text = lang.Code,
				FontSize = 10,
				FontWeight = FontWeights.Bold,
				Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(255, 117, 24) : Color.FromRgb(216, 138, 64))
			};
			Grid.SetColumn(flagBorder, 0);
			grid.Children.Add(flagBorder);

			var nameSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
			nameSp.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = lang.Name,
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(255, 255, 255) : Color.FromRgb(235, 235, 235))
			});
			nameSp.Children.Add(new System.Windows.Controls.TextBlock
			{
				Text = lang.EnglishName,
				FontSize = 10,
				Foreground = new SolidColorBrush(isSelected ? Color.FromRgb(212, 168, 128) : Color.FromRgb(142, 138, 152)),
				Margin = new Thickness(0, 1, 0, 0)
			});
			Grid.SetColumn(nameSp, 1);
			grid.Children.Add(nameSp);

			if (isSelected)
			{
				var chkIcon = new SymbolIcon
				{
					Symbol = SymbolRegular.Checkmark16,
					Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)),
					FontSize = 15,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(6, 0, 2, 0)
				};
				Grid.SetColumn(chkIcon, 2);
				grid.Children.Add(chkIcon);
			}

			if (!isSelected)
			{
				var normalBg = border.Background;
				var normalBrd = border.BorderBrush;
				border.MouseEnter += (s, e) =>
				{
					border.Background = new SolidColorBrush(Color.FromArgb(255, 38, 28, 30));
					border.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 180, 85, 30));
				};
				border.MouseLeave += (s, e) =>
				{
					border.Background = normalBg;
					border.BorderBrush = normalBrd;
				};
			}

			border.Child = grid;
			LanguageProfile targetLang = lang;
			border.MouseLeftButtonUp += (s, ev) =>
			{
				LocalizationEngine.Current = targetLang;
				CloseLanguagePickerAnimated();
			};

			PnlLanguageList.Children.Add(border);
		}
	}

	private static string FormatDuration(double totalSeconds)
	{
		var ts = TimeSpan.FromSeconds(totalSeconds);
		int days = (int)ts.TotalDays;
		if (days > 0)
		{
			return $"{days}d {ts.Hours}h {ts.Minutes}m";
		}
		if (ts.Hours > 0)
		{
			return $"{ts.Hours}h {ts.Minutes}m";
		}
		return $"{ts.Minutes}m {ts.Seconds}s";
	}
}
