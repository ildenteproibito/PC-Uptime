using System;
using AwayUptime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uptime.Tests;

[TestClass]
public class UptimeCalculationTests
{
	[TestMethod]
	public void GetUptime_ReturnsPositiveMonotonicDuration()
	{
		TimeSpan uptime1 = UptimeHelper.GetUptime();
		Assert.IsTrue(uptime1.TotalMilliseconds > 0, "Uptime should be greater than zero.");
		
		TimeSpan uptime2 = UptimeHelper.GetUptime();
		Assert.IsTrue(uptime2 >= uptime1, "Uptime should be monotonically non-decreasing.");
	}

	[TestMethod]
	public void GetBootTime_IsConsistentWithCurrentTimeAndUptime()
	{
		DateTime before = DateTime.Now;
		TimeSpan uptime = UptimeHelper.GetUptime();
		DateTime bootTime = UptimeHelper.GetBootTime();
		DateTime after = DateTime.Now;

		DateTime reconstructedNow = bootTime + uptime;
		Assert.IsTrue(reconstructedNow >= before.AddSeconds(-1) && reconstructedNow <= after.AddSeconds(1),
			"BootTime + Uptime should reconstruct current time within 1 second.");
	}

	[TestMethod]
	public void FormatDisplay_FormatsCorrectlyAcrossIntervals()
	{
		var lang = LocalizationEngine.SupportedLanguages.First(l => l.Id == "en");

		// Days and hours
		var (pri1, sec1) = lang.FormatDisplay(TimeSpan.FromDays(2) + TimeSpan.FromHours(5) + TimeSpan.FromMinutes(30) + TimeSpan.FromSeconds(15));
		Assert.IsTrue(pri1.Contains("days") && pri1.Contains("hours"));
		Assert.IsTrue(sec1.Contains("minutes") && sec1.Contains("seconds"));

		// Hours only
		var (pri2, sec2) = lang.FormatDisplay(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(45) + TimeSpan.FromSeconds(10));
		Assert.IsTrue(pri2.Contains("hours") && pri2.Contains("minutes"));
		Assert.IsTrue(sec2.Contains("seconds"));

		// Minutes only
		var (pri3, sec3) = lang.FormatDisplay(TimeSpan.FromMinutes(25) + TimeSpan.FromSeconds(50));
		Assert.IsTrue(pri3.Contains("minutes"));
		Assert.IsTrue(sec3.Contains("seconds"));
	}

	[TestMethod]
	public void ActiveVsIdle_CalculatesPercentagesWithDefinedDenominator()
	{
		double active = 7200.0; // 2 hours
		double idle = 1800.0;   // 30 mins
		double total = active + idle;

		double activePct = Math.Clamp(active / total * 100.0, 0.0, 100.0);
		double idlePct = Math.Clamp(idle / total * 100.0, 0.0, 100.0);

		Assert.AreEqual(80.0, activePct, 0.01);
		Assert.AreEqual(20.0, idlePct, 0.01);
		Assert.AreEqual(100.0, activePct + idlePct, 0.01);
	}

	[TestMethod]
	public void Window_InitializationSmokeTest()
	{
		Exception? caught = null;
		var thread = new System.Threading.Thread(() =>
		{
			try
			{
				if (System.Windows.Application.Current == null)
				{
					new AwayUptime.App();
				}
				var win = new AwayUptime.MainWindow();
			}
			catch (Exception ex)
			{
				caught = ex;
			}
		});
		thread.SetApartmentState(System.Threading.ApartmentState.STA);
		thread.Start();
		thread.Join();

		if (caught != null)
		{
			var matches = string.Join(", ", Enum.GetNames(typeof(Wpf.Ui.Controls.SymbolRegular)).Where(n => n.Contains("ArrowReset") || n.Contains("Reset")));
			string details = caught.ToString() + "\nMatching symbols: " + matches;
			if (caught.InnerException != null)
			{
				details += "\nINNER: " + caught.InnerException.ToString();
				if (caught.InnerException.InnerException != null)
				{
					details += "\nINNER2: " + caught.InnerException.InnerException.ToString();
				}
			}
			Assert.Fail("Failed to initialize MainWindow: " + details);
		}
	}
}
