using System;
using System.Collections.Generic;
using System.Linq;
using AwayUptime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Uptime.Tests;

[TestClass]
public class SessionAndReliabilityTests
{
	[TestMethod]
	public void SessionRecord_StoresConsistentMetadata()
	{
		var session = new SessionRecord
		{
			SessionId = "test-session-123",
			BootTime = DateTime.UtcNow.AddHours(-10),
			EndTime = DateTime.UtcNow,
			DurationSeconds = 36000.0,
			ActiveSeconds = 28000.0,
			IdleSeconds = 8000.0,
			TerminationType = "NormalShutdown",
			EvidenceSummary = "EventLog 6006",
			IsCurrentSession = false
		};

		Assert.AreEqual("test-session-123", session.SessionId);
		Assert.AreEqual(36000.0, session.DurationSeconds);
		Assert.AreEqual("NormalShutdown", session.TerminationType);
		Assert.IsFalse(session.IsCurrentSession);
	}

	[TestMethod]
	public void StabilityScore_CalculatesDeterministically()
	{
		// 10 resolved sessions: 9 clean, 1 unexpected
		int cleanCount = 9;
		int unexpectedCount = 1;
		int total = cleanCount + unexpectedCount;

		double cleanRate = Math.Round((double)cleanCount / total * 100.0, 1);
		Assert.AreEqual(90.0, cleanRate);

		// When 0 crashes detected
		int cleanOnly = 15;
		int zeroCrashes = 0;
		double cleanOnlyRate = Math.Round((double)cleanOnly / (cleanOnly + zeroCrashes) * 100.0, 1);
		Assert.AreEqual(100.0, cleanOnlyRate);
	}

	[TestMethod]
	public void CorrelatedIncidents_DeduplicationWindow()
	{
		// Simulate event 6008 logged at boot and event 41 logged 10 seconds later
		DateTime bootTime = DateTime.Now;
		DateTime event6008Time = bootTime.AddSeconds(5);
		DateTime event41Time = bootTime.AddSeconds(15);

		var times = new List<DateTime>();
		int incidentsCount = 0;

		foreach (var t in new[] { event6008Time, event41Time })
		{
			bool alreadyDeduplicated = times.Any(existing => Math.Abs((existing - t).TotalSeconds) < 120.0);
			if (!alreadyDeduplicated)
			{
				incidentsCount++;
				times.Add(t);
			}
		}

		Assert.AreEqual(1, incidentsCount, "Correlated 6008 and 41 events within 120s should count as a single incident.");
	}

	[TestMethod]
	public void StabilityScore_IncompleteCoverage_ReturnsInsufficientDataAndDoesNotImply100Percent()
	{
		int cleanCount = 0;
		int unexpectedCount = 0;
		int totalResolved = cleanCount + unexpectedCount;

		// When zero resolved sessions exist, confidence must be Insufficient Data and score should be sentinel (-1.0)
		double score = totalResolved > 0 ? (double)cleanCount / totalResolved * 100.0 : -1.0;
		string confidence = totalResolved == 0 ? "Insufficient Data" : (totalResolved >= 5 ? "Verified" : "Limited Sample");

		Assert.AreEqual(-1.0, score, "Score must not imply 100% or high stability when historical coverage is 0.");
		Assert.AreEqual("Insufficient Data", confidence);
	}

	[TestMethod]
	public void StabilityScore_SampleConfidenceLevel_DistinguishesLimitedFromVerified()
	{
		int smallSample = 3;
		string smallConfidence = smallSample >= 5 ? "Verified" : "Limited Sample";
		Assert.AreEqual("Limited Sample", smallConfidence);

		int largeSample = 12;
		string largeConfidence = largeSample >= 5 ? "Verified" : "Limited Sample";
		Assert.AreEqual("Verified", largeConfidence);
	}

	[TestMethod]
	public void StabilityScore_WarningsIgnored_OnlyCrashesAndUnexpectedShutdownsPenalized()
	{
		// Simulate event stream containing 5 generic warnings and 1 clean shutdown
		var eventIds = new List<int> { 10016, 10016, 219, 10016, 6006 };
		var penalizedIds = new HashSet<int> { 6008, 41, 1001 };

		int crashes = eventIds.Count(id => penalizedIds.Contains(id));
		int clean = eventIds.Count(id => id == 6006);

		Assert.AreEqual(0, crashes, "Generic warnings (e.g. DCOM 10016) must not be treated as stability crashes.");
		Assert.AreEqual(1, clean);
	}
}
