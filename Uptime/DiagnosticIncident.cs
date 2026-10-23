using System;

namespace AwayUptime;

public class DiagnosticIncident
{
	public string IncidentId { get; set; } = Guid.NewGuid().ToString("N");

	public DateTime Timestamp { get; set; }

	public int EventId { get; set; }

	public string ProviderName { get; set; } = string.Empty;

	public string Category { get; set; } = string.Empty;

	public string Explanation { get; set; } = string.Empty;

	public string Confidence { get; set; } = "Verified";

	public string? RelatedSessionId { get; set; }
}
