using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace AwayUptime;

public partial class MiniHudWindow : Window, IComponentConnector
{
	private readonly DispatcherTimer _timer;

	private const string RegistryHudKey = "Software\\Uptime\\HUD";

	public event Action? OnRequestOpenMainWindow;

	public event Action? OnHudStateChanged;

	public MiniHudWindow()
	{
		InitializeComponent();
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		_timer.Tick += (object? s, EventArgs e) =>
		{
			UpdateCounter();
		};
		Loaded += (object s, RoutedEventArgs e) =>
		{
			RestorePosition();
		};
		Closing += (object? s, CancelEventArgs e) =>
		{
			SavePosition();
		};
		ContextMenu = CreateHudContextMenu();
		LocalizationEngine.OnLanguageChanged += UpdateLocalizedTooltips;
		UpdateLocalizedTooltips();
	}

	private void UpdateLocalizedTooltips()
	{
		LanguageProfile current = LocalizationEngine.Current;
		if (BrdHudMain != null) BrdHudMain.ToolTip = current.ToolTipHudDrag;
		if (BtnHudOpen != null) BtnHudOpen.ToolTip = current.ToolTipHudOpen;
		if (BtnHudClose != null) BtnHudClose.ToolTip = current.ToolTipHudClose;
	}

	public void ActivateHud()
	{
		UpdateCounter();
		RestorePosition();
		Show();
		_timer.Start();
		OnHudStateChanged?.Invoke();
	}

	public void DeactivateHud()
	{
		_timer.Stop();
		Hide();
		UptimeHelper.TrimWorkingSet();
		OnHudStateChanged?.Invoke();
	}

	private void UpdateCounter()
	{
		TimeSpan uptime = UptimeHelper.GetUptime();
		int num = (int)uptime.TotalDays;
		LanguageProfile current = LocalizationEngine.Current;
		if (num > 0)
		{
			TxtHudCounter.Text = $"{num}{current.DaysSymbol} {uptime.Hours:D2}{current.HoursSymbol} {uptime.Minutes:D2}{current.MinutesSymbol} {uptime.Seconds:D2}{current.SecondsSymbol}";
		}
		else
		{
			TxtHudCounter.Text = $"{uptime.Hours:D2}{current.HoursSymbol} {uptime.Minutes:D2}{current.MinutesSymbol} {uptime.Seconds:D2}{current.SecondsSymbol}";
		}
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (e.ClickCount == 2)
		{
			OnRequestOpenMainWindow?.Invoke();
			return;
		}
		try
		{
			DragMove();
			SavePosition();
		}
		catch
		{
		}
	}

	private void OpenApp_Click(object sender, RoutedEventArgs e)
	{
		OnRequestOpenMainWindow?.Invoke();
	}

	private void CloseHud_Click(object sender, RoutedEventArgs e)
	{
		DeactivateHud();
	}

	private ContextMenu CreateHudContextMenu()
	{
		ContextMenu contextMenu = new ContextMenu
		{
			Style = (System.Windows.Application.Current.TryFindResource("FluentTrayContextMenuStyle") as Style)
		};
		Style style = System.Windows.Application.Current.TryFindResource("FluentTrayMenuItemStyle") as Style;
		Style style2 = System.Windows.Application.Current.TryFindResource("FluentTrayMenuItemDangerStyle") as Style;
		Style style3 = System.Windows.Application.Current.TryFindResource("FluentTraySeparatorStyle") as Style;
		System.Windows.Controls.MenuItem menuItem = new System.Windows.Controls.MenuItem
		{
			Header = "Mostra Uptime",
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Timer20,
				Foreground = new SolidColorBrush(Color.FromRgb(255, 117, 24)),
				FontSize = 14.0
			}
		};
		menuItem.Click += (object s, RoutedEventArgs e) =>
		{
			OnRequestOpenMainWindow?.Invoke();
		};
		contextMenu.Items.Add(menuItem);
		System.Windows.Controls.MenuItem menuItem2 = new System.Windows.Controls.MenuItem
		{
			Header = "Riposiziona in Alto a Destra",
			Style = style,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.ArrowReset20,
				Foreground = new SolidColorBrush(Color.FromRgb(255, 160, 60)),
				FontSize = 14.0
			}
		};
		menuItem2.Click += (object s, RoutedEventArgs e) =>
		{
			PositionTopRight();
			SavePosition();
		};
		contextMenu.Items.Add(menuItem2);
		contextMenu.Items.Add(new Separator
		{
			Style = style3
		});
		System.Windows.Controls.MenuItem menuItem3 = new System.Windows.Controls.MenuItem
		{
			Header = "Chiudi Mini-HUD",
			Style = style2,
			Icon = new SymbolIcon
			{
				Symbol = SymbolRegular.Dismiss20,
				Foreground = new SolidColorBrush(Color.FromRgb(byte.MaxValue, 120, 120)),
				FontSize = 14.0
			}
		};
		menuItem3.Click += (object s, RoutedEventArgs e) =>
		{
			DeactivateHud();
		};
		contextMenu.Items.Add(menuItem3);
		return contextMenu;
	}

	public void PositionTopRight()
	{
		try
		{
			Screen? obj = Screen.PrimaryScreen ?? Screen.AllScreens[0];
			DpiScale dpi = VisualTreeHelper.GetDpi(this);
			double num = ((dpi.DpiScaleX > 0.0) ? dpi.DpiScaleX : 1.0);
			double num2 = ((dpi.DpiScaleY > 0.0) ? dpi.DpiScaleY : 1.0);
			double num3 = (double)obj.WorkingArea.Right / num;
			double num4 = (double)obj.WorkingArea.Top / num2;
			double num5 = ((ActualWidth > 0.0) ? ActualWidth : Width);
			Left = num3 - num5 - 24.0;
			Top = num4 + 24.0;
		}
		catch
		{
			Rect workArea = SystemParameters.WorkArea;
			Left = workArea.Right - Width - 24.0;
			Top = workArea.Top + 24.0;
		}
	}

	private void RestorePosition()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Uptime\\HUD", writable: false);
			if (registryKey != null)
			{
				object value = registryKey.GetValue("Left");
				object value2 = registryKey.GetValue("Top");
				if (value != null && value2 != null && double.TryParse(value.ToString(), out var result) && double.TryParse(value2.ToString(), out var result2))
				{
					Rect workArea = SystemParameters.WorkArea;
					if (result >= workArea.Left - 50.0 && result <= workArea.Right - 50.0 && result2 >= workArea.Top - 50.0 && result2 <= workArea.Bottom - 50.0)
					{
						Left = result;
						Top = result2;
						return;
					}
				}
			}
		}
		catch
		{
		}
		PositionTopRight();
	}

	private void SavePosition()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Uptime\\HUD");
			registryKey?.SetValue("Left", Left.ToString());
			registryKey?.SetValue("Top", Top.ToString());
		}
		catch
		{
		}
	}
}
