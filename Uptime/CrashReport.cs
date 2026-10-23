using System;
using System.Collections.Generic;

namespace AwayUptime;

public class CrashReport
{
	public int UnexpectedShutdownsLast30Days { get; set; }

	public int CleanShutdownsLast30Days { get; set; }

	public double StabilityScorePercent { get; set; } = 100.0;

	public DateTime? LastCrashDate { get; set; }

	public int TotalObservedSessions { get; set; }

	public double CleanSessionRatePercent { get; set; } = 100.0;

	public string ScoreExplanation { get; set; } = string.Empty;

	public string ConfidenceLevel { get; set; } = "Verified";

	public int EvaluatedDays { get; set; } = 30;

	public List<DiagnosticIncident> Incidents { get; set; } = new List<DiagnosticIncident>();
}
