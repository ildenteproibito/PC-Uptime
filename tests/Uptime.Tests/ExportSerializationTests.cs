using System;
using System.Collections.Generic;
using System.Text.Json;
using AwayUptime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uptime.Tests;

[TestClass]
public class ExportSerializationTests
{
	[TestMethod]
	public void FilterSessionsAndExportJson_ProducesValidJsonWithVersionAndMetadata()
	{
		var sessions = new List<SessionRecord>
		{
			new SessionRecord
			{
				SessionId = "s1",
				BootTime = DateTime.Now.AddDays(-2),
				EndTime = DateTime.Now.AddDays(-1),
				DurationSeconds = 86400,
				ActiveSeconds = 50000,
				IdleSeconds = 36400,
				TerminationType = "NormalShutdown",
				EvidenceSummary = "EventLog 6006"
			}
		};

		var report = new CrashReport
		{
			StabilityScorePercent = 100.0,
			CleanShutdownsLast30Days = 1,
			UnexpectedShutdownsLast30Days = 0,
			CleanSessionRatePercent = 100.0,
			ConfidenceLevel = "Verified"
		};

		string json = ExportHelper.FilterSessionsAndExportJson(sessions, report, "7d");
		Assert.IsFalse(string.IsNullOrEmpty(json));

		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		Assert.IsTrue(root.TryGetProperty("exportMetadata", out var meta));
		Assert.AreEqual("5.1.0", meta.GetProperty("version").GetString());
		Assert.AreEqual("Uptime", meta.GetProperty("application").GetString());

		Assert.IsTrue(root.TryGetProperty("reliabilitySummary", out var rel));
		Assert.AreEqual(100.0, rel.GetProperty("stabilityScorePercent").GetDouble());

		Assert.IsTrue(root.TryGetProperty("sessions", out var sessArr));
		Assert.AreEqual(1, sessArr.GetArrayLength());
		Assert.AreEqual("s1", sessArr[0].GetProperty("sessionId").GetString());
	}

	[TestMethod]
	public void FilterSessionsAndExportCsv_ProducesValidRfc4180EscapedCsv()
	{
		var sessions = new List<SessionRecord>
		{
			new SessionRecord
			{
				SessionId = "s1",
				BootTime = new DateTime(2026, 10, 10, 8, 30, 0),
				EndTime = new DateTime(2026, 10, 10, 18, 30, 0),
				DurationSeconds = 36000,
				ActiveSeconds = 30000,
				IdleSeconds = 6000,
				TerminationType = "NormalShutdown",
				EvidenceSummary = "EventLog 6006, Clean shutdown", // Contains comma to test RFC 4180 escaping
				IsCurrentSession = false
			}
		};

		string csv = ExportHelper.FilterSessionsAndExportCsv(sessions, "all");
		Assert.IsFalse(string.IsNullOrEmpty(csv));

		string[] lines = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
		Assert.AreEqual(2, lines.Length, "Should have 1 header line and 1 data line.");

		// Verify header line has expected columns
		StringAssert.Contains(lines[0], "SessionId");
		StringAssert.Contains(lines[0], "BootTimeLocal");
		StringAssert.Contains(lines[0], "DurationHours");
		StringAssert.Contains(lines[0], "Evidence");

		// Verify evidence is quoted due to comma
		StringAssert.Contains(lines[1], "\"EventLog 6006, Clean shutdown\"");
	}

	[TestMethod]
	public void FilterCutoff_CalculatesCorrectTimeframes()
	{
		DateTime now = DateTime.Now;

		DateTime cutoff24h = ExportHelper.GetFilterCutoff("24h");
		Assert.IsTrue(cutoff24h >= now.AddHours(-25) && cutoff24h <= now.AddHours(-23));

		DateTime cutoff7d = ExportHelper.GetFilterCutoff("7d");
		Assert.IsTrue(cutoff7d >= now.AddDays(-8) && cutoff7d <= now.AddDays(-6));

		DateTime cutoffAll = ExportHelper.GetFilterCutoff("all");
		Assert.AreEqual(DateTime.MinValue, cutoffAll);
	}
}
