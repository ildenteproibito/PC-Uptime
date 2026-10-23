using System;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AwayUptime;

public static class BadgeGenerator
{
	public static RenderTargetBitmap RenderBadge(LanguageProfile lang)
	{
		TimeSpan uptime = UptimeHelper.GetUptime();
		DateTime bootTime = UptimeHelper.GetBootTime();
		(string primary, string secondary) tuple = lang.FormatDisplay(uptime);
		string item = tuple.primary;
		string item2 = tuple.secondary;
		TimeSpan item3 = SystemMetricsTracker.GetPersonalBest().maxUptime;
		string item4 = lang.FormatDisplay(item3).primary;
		string bootDurationString = SystemMetricsTracker.GetBootDurationString();
		CrashReport crashReport = SystemMetricsTracker.GetCrashReport();
		bool flag = SystemMetricsTracker.IsFastStartupEnabled();
		Border border = new Border
		{
			Width = 560.0,
			Height = 310.0,
			Background = new LinearGradientBrush(Color.FromRgb(13, 17, 23), Color.FromRgb(22, 27, 38), new Point(0.0, 0.0), new Point(1.0, 1.0)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 72)),
			BorderThickness = new Thickness(1.5),
			CornerRadius = new CornerRadius(14.0),
			Padding = new Thickness(22.0, 16.0, 22.0, 14.0),
			SnapsToDevicePixels = true
		};
		TextOptions.SetTextFormattingMode(border, TextFormattingMode.Display);
		TextOptions.SetTextRenderingMode(border, TextRenderingMode.ClearType);
		Grid grid = new Grid();
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		Grid grid2 = new Grid();
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element = new TextBlock
		{
			Text = "UPTIME",
			FontSize = 15.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(76, 194, byte.MaxValue)),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element2 = new TextBlock
		{
			Text = lang.BadgeSubBrand,
			FontSize = 10.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel.Children.Add(element);
		stackPanel.Children.Add(element2);
		grid2.Children.Add(stackPanel);
		Border border2 = new Border
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
			Background = new SolidColorBrush(Color.FromArgb(220, 19, 43, 32)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(35, 134, 54)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(6.0),
			Padding = new Thickness(8.0, 3.0, 8.0, 3.0)
		};
		TextBlock child = new TextBlock
		{
			Text = lang.BadgeVerified,
			FontSize = 9.5,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80))
		};
		border2.Child = child;
		grid2.Children.Add(border2);
		Grid.SetRow(grid2, 0);
		grid.Children.Add(grid2);
		Border element3 = new Border
		{
			Height = 1.0,
			Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
			Margin = new Thickness(0.0, 10.0, 0.0, 12.0)
		};
		Grid.SetRow(element3, 1);
		grid.Children.Add(element3);
		Grid grid3 = new Grid();
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.25, GridUnitType.Star)
		});
		grid3.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		StackPanel stackPanel2 = new StackPanel
		{
			Margin = new Thickness(0.0, 0.0, 14.0, 0.0)
		};
		TextBlock element4 = new TextBlock
		{
			Text = lang.BadgeActiveSession,
			FontSize = 9.5,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
		};
		TextBlock element5 = new TextBlock
		{
			Text = item,
			FontSize = 19.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243))
		};
		TextBlock element6 = new TextBlock
		{
			Text = item2,
			FontSize = 12.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(88, 166, byte.MaxValue)),
			Margin = new Thickness(0.0, 1.0, 0.0, 10.0)
		};
		TextBlock element7 = new TextBlock
		{
			Text = lang.PersonalBestHeader,
			FontSize = 9.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158)),
			Margin = new Thickness(0.0, 0.0, 0.0, 2.0)
		};
		TextBlock element8 = new TextBlock
		{
			Text = "\ud83c\udfc6 " + item4,
			FontSize = 12.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(210, 153, 34)),
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		TextBlock element9 = new TextBlock
		{
			Text = lang.LastBootHeader + ": " + bootTime.ToString("d MMMM yyyy, HH:mm", lang.Culture),
			FontSize = 10.0,
			Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
		};
		stackPanel2.Children.Add(element4);
		stackPanel2.Children.Add(element5);
		stackPanel2.Children.Add(element6);
		stackPanel2.Children.Add(element7);
		stackPanel2.Children.Add(element8);
		stackPanel2.Children.Add(element9);
		Grid.SetColumn(stackPanel2, 0);
		grid3.Children.Add(stackPanel2);
		Border border3 = new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(22, 27, 34)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(48, 54, 61)),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(10.0),
			Padding = new Thickness(12.0, 10.0, 12.0, 10.0)
		};
		StackPanel stackPanel3 = new StackPanel();
		TextBlock element10 = new TextBlock
		{
			Text = lang.StabilityRateLabel,
			FontSize = 9.5,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(139, 148, 158))
		};
		TextBlock element11 = new TextBlock
		{
			Text = $"{crashReport.StabilityScorePercent:F1}%",
			FontSize = 26.0,
			FontWeight = FontWeights.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(63, 185, 80)),
			Margin = new Thickness(0.0, 1.0, 0.0, 1.0)
		};
		string text = ((crashReport.UnexpectedShutdownsLast30Days == 0) ? lang.NoCrashesMessage : string.Format(lang.CrashesDetectedFormat, crashReport.UnexpectedShutdownsLast30Days));
		TextBlock element12 = new TextBlock
		{
			Text = text,
			FontSize = 10.0,
			Foreground = new SolidColorBrush((crashReport.UnexpectedShutdownsLast30Days == 0) ? Color.FromRgb(126, 231, 135) : Color.FromRgb(248, 81, 73)),
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		Border element13 = new Border
		{
			Height = 1.0,
			Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
			Margin = new Thickness(0.0, 2.0, 0.0, 6.0)
		};
		string text2 = ((bootDurationString == "N/D") ? lang.NotAvailable : bootDurationString);
		TextBlock element14 = new TextBlock
		{
			Text = "⚡ " + lang.BootDurationHeader + ": " + text2,
			FontSize = 10.5,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(230, 237, 243))
		};
		TextBlock element15 = new TextBlock
		{
			Text = (flag ? ("⚠\ufe0f " + lang.FastStartupTitle + ": " + lang.FastStartupStatusActive) : ("✓ " + lang.FastStartupTitle + ": " + lang.FastStartupStatusDisabled)),
			FontSize = 9.5,
			TextWrapping = TextWrapping.Wrap,
			Foreground = new SolidColorBrush(flag ? Color.FromRgb(210, 153, 34) : Color.FromRgb(63, 185, 80)),
			Margin = new Thickness(0.0, 3.0, 0.0, 0.0)
		};
		stackPanel3.Children.Add(element10);
		stackPanel3.Children.Add(element11);
		stackPanel3.Children.Add(element12);
		stackPanel3.Children.Add(element13);
		stackPanel3.Children.Add(element14);
		stackPanel3.Children.Add(element15);
		border3.Child = stackPanel3;
		Grid.SetColumn(border3, 1);
		grid3.Children.Add(border3);
		Grid.SetRow(grid3, 2);
		grid.Children.Add(grid3);
		Border element16 = new Border
		{
			Height = 1.0,
			Background = new SolidColorBrush(Color.FromRgb(33, 38, 45)),
			Margin = new Thickness(0.0, 10.0, 0.0, 6.0)
		};
		Grid.SetRow(element16, 3);
		grid.Children.Add(element16);
		Grid grid4 = new Grid();
		TextBlock element17 = new TextBlock
		{
			Text = "Uptime for Windows • Crafted by IL DENTE PROIBITO",
			FontSize = 9.0,
			Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock element18 = new TextBlock
		{
			Text = "r/pcmasterrace • github.com/ildenteproibito",
			FontSize = 9.0,
			Foreground = new SolidColorBrush(Color.FromRgb(110, 118, 129)),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		grid4.Children.Add(element17);
		grid4.Children.Add(element18);
		Grid.SetRow(grid4, 4);
		grid.Children.Add(grid4);
		border.Child = grid;
		border.Measure(new Size(560.0, 310.0));
		border.Arrange(new Rect(0.0, 0.0, 560.0, 310.0));
		border.UpdateLayout();
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(560, 310, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(border);
		return renderTargetBitmap;
	}

	public static bool CopyBadgeToClipboard(LanguageProfile lang)
	{
		try
		{
			RenderTargetBitmap renderTargetBitmap = RenderBadge(lang);
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
			string text = Path.Combine(Path.GetTempPath(), $"Uptime_Badge_{DateTime.Now:yyyyMMdd_HHmmss}.png");
			using (FileStream stream = new FileStream(text, FileMode.Create))
			{
				pngBitmapEncoder.Save(stream);
			}
			DataObject dataObject = new DataObject();
			dataObject.SetImage(renderTargetBitmap);
			StringCollection fileDropList = new StringCollection { text };
			dataObject.SetFileDropList(fileDropList);
			Clipboard.SetDataObject(dataObject, copy: true);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
