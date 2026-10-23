using System;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;

namespace AwayUptime;

public partial class NightPromptWindow : Window, IComponentConnector
{
	private readonly DispatcherTimer _autoDismissTimer;

	public NightPromptWindow()
	{
		InitializeComponent();
		_autoDismissTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMinutes(3.0)
		};
		_autoDismissTimer.Tick += (object? s, EventArgs e) =>
		{
			_autoDismissTimer.Stop();
			Close();
		};
		ApplyLocalization();
		LocalizationEngine.OnLanguageChanged += ApplyLocalization;
	}

	public void ShowPrompt()
	{
		ApplyLocalization();
		Rect workArea = SystemParameters.WorkArea;
		Left = workArea.Right - Width - 16.0;
		Top = workArea.Bottom - Height - 16.0;
		Show();
		Activate();
		_autoDismissTimer.Start();
	}

	private void ApplyLocalization()
	{
		LanguageProfile current = LocalizationEngine.Current;
		TxtTitle.Text = current.NightPromptTitle;
		TxtMessage.Text = current.NightPromptMessage;
		BtnShutdown.Content = current.BtnNightShutdown;
		BtnStayOn.Content = current.BtnNightStayOn;
	}

	private void BtnStayOn_Click(object sender, RoutedEventArgs e)
	{
		_autoDismissTimer.Stop();
		SystemMetricsTracker.DismissNightPrompt();
		Close();
	}

	private void BtnShutdown_Click(object sender, RoutedEventArgs e)
	{
		_autoDismissTimer.Stop();
		Close();
		SystemMetricsTracker.ShutdownForNight();
	}

	protected override void OnClosed(EventArgs e)
	{
		LocalizationEngine.OnLanguageChanged -= ApplyLocalization;
		_autoDismissTimer.Stop();
		base.OnClosed(e);
	}
}
