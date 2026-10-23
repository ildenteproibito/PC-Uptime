using System;
using System.Linq;
using System.Text.Json;
using AwayUptime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uptime.Tests;

[TestClass]
public class DashboardConfigTests
{
	[TestMethod]
	public void CreateDefault_HasExpectedInitialWidgets()
	{
		var config = DashboardConfig.CreateDefault();
		Assert.AreEqual(DashboardConfig.PresetDefault, config.ActivePreset);

		var visible = config.Widgets.Where(w => w.IsVisible).Select(w => w.WidgetId).ToList();
		Assert.AreEqual(4, visible.Count);
		CollectionAssert.AreEqual(new[] { "uptime", "boot_time", "active_idle", "health_advisor" }, visible);

		// All 12 widgets must be registered
		Assert.AreEqual(12, config.Widgets.Count);
	}

	[TestMethod]
	public void CreateMinimal_ActivatesMinimalPresetWidgets()
	{
		var config = DashboardConfig.CreateMinimal();
		Assert.AreEqual(DashboardConfig.PresetMinimal, config.ActivePreset);

		var visible = config.Widgets.Where(w => w.IsVisible).Select(w => w.WidgetId).ToList();
		CollectionAssert.AreEqual(new[] { "uptime", "boot_time", "system_info" }, visible);
	}

	[TestMethod]
	public void CreateReliability_ActivatesReliabilityPresetWidgets()
	{
		var config = DashboardConfig.CreateReliability();
		Assert.AreEqual(DashboardConfig.PresetReliability, config.ActivePreset);

		var visible = config.Widgets.Where(w => w.IsVisible).Select(w => w.WidgetId).ToList();
		CollectionAssert.AreEqual(new[] { "uptime", "health_advisor", "stability_score", "reliability_summary", "crashes_summary", "current_session" }, visible);
	}

	[TestMethod]
	public void MoveWidget_ReordersContiguouslyAndSetsCustomPreset()
	{
		var config = DashboardConfig.CreateDefault();
		string firstWidget = config.Widgets[0].WidgetId;
		string secondWidget = config.Widgets[1].WidgetId;

		bool moved = config.MoveWidget(secondWidget, moveUp: true);
		Assert.IsTrue(moved);
		Assert.AreEqual(secondWidget, config.Widgets[0].WidgetId);
		Assert.AreEqual(firstWidget, config.Widgets[1].WidgetId);
		Assert.AreEqual(DashboardConfig.PresetCustom, config.ActivePreset);

		// Cannot move top item up
		Assert.IsFalse(config.MoveWidget(secondWidget, moveUp: true));
	}

	[TestMethod]
	public void SetWidgetVisibility_UpdatesVisibilityAndMarksCustom()
	{
		var config = DashboardConfig.CreateDefault();
		config.SetWidgetVisibility("system_info", true);

		Assert.IsTrue(config.Widgets.First(w => w.WidgetId == "system_info").IsVisible);
		Assert.AreEqual(DashboardConfig.PresetCustom, config.ActivePreset);

		config.SetWidgetVisibility("uptime", false);
		Assert.IsFalse(config.Widgets.First(w => w.WidgetId == "uptime").IsVisible);
	}

	[TestMethod]
	public void DashboardSerialization_PreservesPreferencesAcrossRestart()
	{
		var config = DashboardConfig.CreateDefault();
		config.SetWidgetVisibility("system_info", true);
		config.MoveWidget("system_info", moveUp: true);

		string json = JsonSerializer.Serialize(config);
		var deserialized = JsonSerializer.Deserialize<DashboardConfig>(json);

		Assert.IsNotNull(deserialized);
		Assert.AreEqual(config.ActivePreset, deserialized.ActivePreset);
		Assert.AreEqual(config.Widgets.Count, deserialized.Widgets.Count);
		Assert.IsTrue(deserialized.Widgets.First(w => w.WidgetId == "system_info").IsVisible);
	}

	[TestMethod]
	public void MigrationFrom100_NullDashboardDeserializesSafely()
	{
		// 1.0.0 JSON only had MaxUptimeSeconds, MaxUptimeDate, RecentSessions
		string v100Json = @"{
			""MaxUptimeSeconds"": 12345.6,
			""MaxUptimeDate"": ""2026-10-12T10:00:00"",
			""RecentSessions"": []
		}";

		var stats = JsonSerializer.Deserialize<UptimeStats>(v100Json);
		Assert.IsNotNull(stats);
		Assert.AreEqual(12345.6, stats.MaxUptimeSeconds);
		Assert.IsNotNull(stats.Dashboard);
		Assert.AreEqual(DashboardConfig.PresetDefault, stats.Dashboard.ActivePreset);
		Assert.AreEqual(4, stats.Dashboard.Widgets.Count(w => w.IsVisible));
	}
}
