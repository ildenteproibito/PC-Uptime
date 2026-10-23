using System;
using System.Collections.Generic;
using System.Linq;

namespace AwayUptime;

public class DashboardWidgetItem
{
	public string WidgetId { get; set; } = string.Empty;

	public bool IsVisible { get; set; } = true;

	public int Order { get; set; }

	public DashboardWidgetItem() { }

	public DashboardWidgetItem(string widgetId, bool isVisible, int order)
	{
		WidgetId = widgetId;
		IsVisible = isVisible;
		Order = order;
	}
}

public class DashboardConfig
{
	public const string PresetDefault = "Default";
	public const string PresetMinimal = "Minimal";
	public const string PresetReliability = "Reliability";
	public const string PresetCustom = "Custom";

	public static readonly string[] AllWidgetIds = new[]
	{
		"uptime",
		"boot_time",
		"active_idle",
		"health_advisor",
		"reliability_summary",
		"crashes_summary",
		"current_session",
		"session_history_preview",
		"stability_score",
		"longest_session",
		"observed_sessions",
		"system_info"
	};

	public string ActivePreset { get; set; } = PresetDefault;

	public List<DashboardWidgetItem> Widgets { get; set; } = new List<DashboardWidgetItem>();

	public static DashboardConfig CreateDefault()
	{
		var config = new DashboardConfig
		{
			ActivePreset = PresetDefault,
			Widgets = new List<DashboardWidgetItem>
			{
				new DashboardWidgetItem("uptime", true, 0),
				new DashboardWidgetItem("boot_time", true, 1),
				new DashboardWidgetItem("active_idle", true, 2),
				new DashboardWidgetItem("health_advisor", true, 3),
				new DashboardWidgetItem("reliability_summary", false, 4),
				new DashboardWidgetItem("crashes_summary", false, 5),
				new DashboardWidgetItem("current_session", false, 6),
				new DashboardWidgetItem("session_history_preview", false, 7),
				new DashboardWidgetItem("stability_score", false, 8),
				new DashboardWidgetItem("longest_session", false, 9),
				new DashboardWidgetItem("observed_sessions", false, 10),
				new DashboardWidgetItem("system_info", false, 11)
			}
		};
		return config;
	}

	public static DashboardConfig CreateMinimal()
	{
		var config = new DashboardConfig
		{
			ActivePreset = PresetMinimal,
			Widgets = new List<DashboardWidgetItem>
			{
				new DashboardWidgetItem("uptime", true, 0),
				new DashboardWidgetItem("boot_time", true, 1),
				new DashboardWidgetItem("system_info", true, 2),
				new DashboardWidgetItem("active_idle", false, 3),
				new DashboardWidgetItem("health_advisor", false, 4),
				new DashboardWidgetItem("reliability_summary", false, 5),
				new DashboardWidgetItem("crashes_summary", false, 6),
				new DashboardWidgetItem("current_session", false, 7),
				new DashboardWidgetItem("session_history_preview", false, 8),
				new DashboardWidgetItem("stability_score", false, 9),
				new DashboardWidgetItem("longest_session", false, 10),
				new DashboardWidgetItem("observed_sessions", false, 11)
			}
		};
		return config;
	}

	public static DashboardConfig CreateReliability()
	{
		var config = new DashboardConfig
		{
			ActivePreset = PresetReliability,
			Widgets = new List<DashboardWidgetItem>
			{
				new DashboardWidgetItem("uptime", true, 0),
				new DashboardWidgetItem("health_advisor", true, 1),
				new DashboardWidgetItem("stability_score", true, 2),
				new DashboardWidgetItem("reliability_summary", true, 3),
				new DashboardWidgetItem("crashes_summary", true, 4),
				new DashboardWidgetItem("current_session", true, 5),
				new DashboardWidgetItem("boot_time", false, 6),
				new DashboardWidgetItem("active_idle", false, 7),
				new DashboardWidgetItem("session_history_preview", false, 8),
				new DashboardWidgetItem("longest_session", false, 9),
				new DashboardWidgetItem("observed_sessions", false, 10),
				new DashboardWidgetItem("system_info", false, 11)
			}
		};
		return config;
	}

	public void ApplyPreset(string presetName)
	{
		DashboardConfig template = presetName switch
		{
			PresetMinimal => CreateMinimal(),
			PresetReliability => CreateReliability(),
			_ => CreateDefault()
		};

		ActivePreset = presetName;
		Widgets = template.Widgets;
	}

	public void EnsureAllWidgetsExist()
	{
		if (Widgets == null)
		{
			Widgets = new List<DashboardWidgetItem>();
		}

		var existingIds = new HashSet<string>(Widgets.Select(w => w.WidgetId));
		int nextOrder = Widgets.Count > 0 ? Widgets.Max(w => w.Order) + 1 : 0;

		foreach (var id in AllWidgetIds)
		{
			if (!existingIds.Contains(id))
			{
				Widgets.Add(new DashboardWidgetItem(id, false, nextOrder++));
			}
		}

		// Normalize orders to be contiguous 0..N-1
		var sorted = Widgets.OrderBy(w => w.Order).ToList();
		for (int i = 0; i < sorted.Count; i++)
		{
			sorted[i].Order = i;
		}
		Widgets = sorted;
	}

	public bool MoveWidget(string widgetId, bool moveUp)
	{
		EnsureAllWidgetsExist();
		int index = Widgets.FindIndex(w => w.WidgetId == widgetId);
		if (index < 0) return false;

		int targetIndex = moveUp ? index - 1 : index + 1;
		if (targetIndex < 0 || targetIndex >= Widgets.Count) return false;

		var item = Widgets[index];
		Widgets.RemoveAt(index);
		Widgets.Insert(targetIndex, item);

		for (int i = 0; i < Widgets.Count; i++)
		{
			Widgets[i].Order = i;
		}

		ActivePreset = PresetCustom;
		return true;
	}

	public void SetWidgetVisibility(string widgetId, bool isVisible)
	{
		EnsureAllWidgetsExist();
		var widget = Widgets.FirstOrDefault(w => w.WidgetId == widgetId);
		if (widget != null)
		{
			widget.IsVisible = isVisible;
			ActivePreset = PresetCustom;
		}
	}
}
