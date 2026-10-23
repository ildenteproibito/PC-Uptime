using System;

namespace AwayUptime;

public class SessionRecord
{
	public string SessionId { get; set; } = Guid.NewGuid().ToString("N");

	public DateTime BootTime { get; set; }

	public DateTime? EndTime { get; set; }

	public double DurationSeconds { get; set; }

	public double ActiveSeconds { get; set; }

	public double IdleSeconds { get; set; }

	public string TerminationType { get; set; } = "CurrentSession";

	public string EvidenceSummary { get; set; } = string.Empty;

	public bool IsCurrentSession { get; set; }

	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

	public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
