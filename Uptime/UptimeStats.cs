using System;
using System.Collections.Generic;

namespace AwayUptime;

public class UptimeStats
{
	public double MaxUptimeSeconds { get; set; }

	public DateTime MaxUptimeDate { get; set; }

	public List<SessionRecord> RecentSessions { get; set; } = new List<SessionRecord>();

	public DashboardConfig Dashboard { get; set; } = DashboardConfig.CreateDefault();

	public List<DiagnosticIncident> Incidents { get; set; } = new List<DiagnosticIncident>();
}
