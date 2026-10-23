using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace AwayUptime;

public static class ExportHelper
{
	public static string FilterSessionsAndExportJson(
		IEnumerable<SessionRecord> sessions,
		CrashReport crashReport,
		string timeFilter,
		string? destinationPath = null)
	{
		DateTime cutoff = GetFilterCutoff(timeFilter);
		var filteredSessions = sessions
			.Where(s => s.BootTime >= cutoff)
			.OrderByDescending(s => s.BootTime)
			.ToList();

		var exportModel = new
		{
			exportMetadata = new
			{
				application = "Uptime",
				version = typeof(ExportHelper).Assembly.GetName().Version?.ToString(3) ?? "5.1.0",
				exportDateUtc = DateTime.UtcNow.ToString("o"),
				exportDateLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
				filterPeriod = timeFilter,
				filterCutoffUtc = cutoff.ToUniversalTime().ToString("o")
			},
			reliabilitySummary = new
			{
				stabilityScorePercent = crashReport.StabilityScorePercent,
				cleanShutdowns = crashReport.CleanShutdownsLast30Days,
				unexpectedShutdowns = crashReport.UnexpectedShutdownsLast30Days,
				totalObservedSessions = filteredSessions.Count,
				cleanSessionRatePercent = crashReport.CleanSessionRatePercent,
				confidenceLevel = crashReport.ConfidenceLevel,
				scoreExplanation = crashReport.ScoreExplanation,
				lastCrashDate = crashReport.LastCrashDate?.ToString("o")
			},
			sessions = filteredSessions.Select(s => new
			{
				sessionId = s.SessionId,
				bootTimeUtc = s.BootTime.ToUniversalTime().ToString("o"),
				bootTimeLocal = s.BootTime.ToString("yyyy-MM-dd HH:mm:ss"),
				endTimeUtc = s.EndTime?.ToUniversalTime().ToString("o"),
				endTimeLocal = s.EndTime?.ToString("yyyy-MM-dd HH:mm:ss"),
				durationSeconds = Math.Round(s.DurationSeconds, 1),
				durationFormatted = FormatDuration(s.DurationSeconds),
				activeSeconds = Math.Round(s.ActiveSeconds, 1),
				idleSeconds = Math.Round(s.IdleSeconds, 1),
				terminationType = s.TerminationType,
				evidenceSummary = s.EvidenceSummary,
				isCurrentSession = s.IsCurrentSession
			}).ToList(),
			diagnosticIncidents = crashReport.Incidents
				.Where(i => i.Timestamp >= cutoff)
				.OrderByDescending(i => i.Timestamp)
				.Select(i => new
				{
					incidentId = i.IncidentId,
					timestampUtc = i.Timestamp.ToUniversalTime().ToString("o"),
					timestampLocal = i.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
					eventId = i.EventId,
					provider = i.ProviderName,
					category = i.Category,
					explanation = i.Explanation,
					confidence = i.Confidence,
					relatedSessionId = i.RelatedSessionId
				}).ToList()
		};

		var options = new JsonSerializerOptions
		{
			WriteIndented = true
		};

		string json = JsonSerializer.Serialize(exportModel, options);

		if (!string.IsNullOrEmpty(destinationPath))
		{
			File.WriteAllText(destinationPath, json, new UTF8Encoding(false));
		}

		return json;
	}

	public static string FilterSessionsAndExportCsv(
		IEnumerable<SessionRecord> sessions,
		string timeFilter,
		string? destinationPath = null)
	{
		DateTime cutoff = GetFilterCutoff(timeFilter);
		var filtered = sessions
			.Where(s => s.BootTime >= cutoff)
			.OrderByDescending(s => s.BootTime)
			.ToList();

		var sb = new StringBuilder();
		// Header row
		sb.AppendLine("SessionId,BootTimeLocal,BootTimeUtc,EndTimeLocal,EndTimeUtc,DurationHours,DurationFormatted,ActiveHours,IdleHours,TerminationType,Evidence,IsCurrentSession");

		foreach (var s in filtered)
		{
			string bootLocal = s.BootTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			string bootUtc = s.BootTime.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
			string endLocal = s.EndTime.HasValue ? s.EndTime.Value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty;
			string endUtc = s.EndTime.HasValue ? s.EndTime.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : string.Empty;
			string durationHours = (s.DurationSeconds / 3600.0).ToString("F2", CultureInfo.InvariantCulture);
			string durationFmt = FormatDuration(s.DurationSeconds);
			string activeHours = (s.ActiveSeconds / 3600.0).ToString("F2", CultureInfo.InvariantCulture);
			string idleHours = (s.IdleSeconds / 3600.0).ToString("F2", CultureInfo.InvariantCulture);

			sb.Append(EscapeCsv(s.SessionId)).Append(',')
			  .Append(EscapeCsv(bootLocal)).Append(',')
			  .Append(EscapeCsv(bootUtc)).Append(',')
			  .Append(EscapeCsv(endLocal)).Append(',')
			  .Append(EscapeCsv(endUtc)).Append(',')
			  .Append(durationHours).Append(',')
			  .Append(EscapeCsv(durationFmt)).Append(',')
			  .Append(activeHours).Append(',')
			  .Append(idleHours).Append(',')
			  .Append(EscapeCsv(s.TerminationType)).Append(',')
			  .Append(EscapeCsv(s.EvidenceSummary)).Append(',')
			  .Append(s.IsCurrentSession ? "True" : "False")
			  .AppendLine();
		}

		string csv = sb.ToString();

		if (!string.IsNullOrEmpty(destinationPath))
		{
			// UTF-8 with BOM for compatibility with Excel
			File.WriteAllText(destinationPath, csv, Encoding.UTF8);
		}

		return csv;
	}

	public static bool PromptAndExportJson(IEnumerable<SessionRecord> sessions, CrashReport crashReport, string timeFilter, out string selectedPath)
	{
		selectedPath = string.Empty;
		try
		{
			var sfd = new SaveFileDialog
			{
				Title = "Export Session History (JSON)",
				Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
				FileName = $"Uptime_Sessions_{DateTime.Now:yyyyMMdd_HHmmss}.json",
				DefaultExt = ".json",
				OverwritePrompt = true
			};

			if (sfd.ShowDialog() == true)
			{
				selectedPath = sfd.FileName;
				FilterSessionsAndExportJson(sessions, crashReport, timeFilter, selectedPath);
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool PromptAndExportCsv(IEnumerable<SessionRecord> sessions, string timeFilter, out string selectedPath)
	{
		selectedPath = string.Empty;
		try
		{
			var sfd = new SaveFileDialog
			{
				Title = "Export Session History (CSV)",
				Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
				FileName = $"Uptime_Sessions_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
				DefaultExt = ".csv",
				OverwritePrompt = true
			};

			if (sfd.ShowDialog() == true)
			{
				selectedPath = sfd.FileName;
				FilterSessionsAndExportCsv(sessions, timeFilter, selectedPath);
				return true;
			}
		}
		catch
		{
		}
		return false;
	}

	public static DateTime GetFilterCutoff(string filter)
	{
		return filter switch
		{
			"24h" => DateTime.Now.AddHours(-24),
			"7d" => DateTime.Now.AddDays(-7),
			"30d" => DateTime.Now.AddDays(-30),
			_ => DateTime.MinValue
		};
	}

	private static string EscapeCsv(string? field)
	{
		if (string.IsNullOrEmpty(field)) return string.Empty;
		if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
		{
			return $"\"{field.Replace("\"", "\"\"")}\"";
		}
		return field;
	}

	private static string FormatDuration(double totalSeconds)
	{
		var ts = TimeSpan.FromSeconds(totalSeconds);
		int days = (int)ts.TotalDays;
		if (days > 0)
		{
			return $"{days}d {ts.Hours}h {ts.Minutes}m";
		}
		return $"{ts.Hours}h {ts.Minutes}m {ts.Seconds}s";
	}
}
