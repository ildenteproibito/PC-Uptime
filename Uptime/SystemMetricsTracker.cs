using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace AwayUptime;

public static class SystemMetricsTracker
{
	private struct LASTINPUTINFO
	{
		public uint cbSize;
		public uint dwTime;
	}

	private static readonly string StatsFolder;
	private static readonly string StatsFile;

	private static UptimeStats _stats;
	private static double _activeSeconds;
	private static double _idleSeconds;
	private static double _continuousActiveSeconds;
	private static string? _cachedBootDuration;
	private static bool _timerActive;
	private static int _lastNightPromptDay;
	private static int _lastMaintenancePromptDay;
	private static readonly HashSet<int> ReachedMilestones;
	private static CrashReport? _cachedCrashReport;
	private static DateTime _lastCrashQueryTime;

	public static bool IsShutdownTimerActive => _timerActive;

	public static event Action? OnStatsUpdated;
	public static event Action<string>? OnHealthBreakRecommended;
	public static event Action<string>? OnMilestoneReached;
	public static event Action? OnNightIdleDetected;
	public static event Action<int>? OnMaintenanceRecommended;

	[DllImport("user32.dll")]
	private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

	static SystemMetricsTracker()
	{
		StatsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Uptime");
		StatsFile = Path.Combine(StatsFolder, "stats.json");
		_stats = new UptimeStats();
		_activeSeconds = 0.0;
		_idleSeconds = 0.0;
		_continuousActiveSeconds = 0.0;
		_cachedBootDuration = null;
		_timerActive = false;
		_lastNightPromptDay = -1;
		_lastMaintenancePromptDay = -1;
		ReachedMilestones = new HashSet<int>();
		_lastCrashQueryTime = DateTime.MinValue;

		LoadStats();
		ReconcileCurrentSession();
	}

	public static string GetBootDurationString()
	{
		if (_cachedBootDuration != null)
		{
			return _cachedBootDuration;
		}
		try
		{
			string query = "*[System[(EventID=30) and Provider[@Name='Microsoft-Windows-Kernel-Boot']]]";
			using EventLogReader reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, query)
			{
				ReverseDirection = true
			});
			EventRecord eventRecord = reader.ReadEvent();
			if (eventRecord != null)
			{
				string text = eventRecord.ToXml();
				int num = text.IndexOf("ExitBootServicesExit");
				if (num > 0)
				{
					int num2 = text.IndexOf(">", num) + 1;
					int num3 = text.IndexOf("<", num2);
					if (num2 > 0 && num3 > num2 && long.TryParse(text.Substring(num2, num3 - num2).Trim(), out var result) && result > 0)
					{
						double value = (double)result / 1000.0;
						_cachedBootDuration = $"{value:F1}s";
						return _cachedBootDuration;
					}
				}
			}
		}
		catch
		{
		}
		_cachedBootDuration = "N/D";
		return _cachedBootDuration;
	}

	public static void TickTracker(int intervalSeconds)
	{
		LASTINPUTINFO plii = new LASTINPUTINFO
		{
			cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
		};

		if (GetLastInputInfo(ref plii))
		{
			uint num = (uint)Environment.TickCount - plii.dwTime;
			if (num > 150000) // 2.5 minutes without input = idle
			{
				_idleSeconds += intervalSeconds;
				_continuousActiveSeconds = Math.Max(0.0, _continuousActiveSeconds - (double)intervalSeconds);
				int hour = DateTime.Now.Hour;
				if ((hour >= 23 || hour < 7) && num >= 1800000)
				{
					int dayOfYear = DateTime.Now.DayOfYear;
					if (_lastNightPromptDay != dayOfYear)
					{
						_lastNightPromptDay = dayOfYear;
						OnNightIdleDetected?.Invoke();
					}
				}
			}
			else
			{
				_activeSeconds += intervalSeconds;
				_continuousActiveSeconds += intervalSeconds;
				if (_continuousActiveSeconds >= 10800.0) // 3 hours
				{
					OnHealthBreakRecommended?.Invoke("3h");
					_continuousActiveSeconds = 0.0;
				}
			}
		}
		else
		{
			_activeSeconds += intervalSeconds;
		}

		// Update current session record
		UpdateCurrentSessionRecord();

		double totalSeconds = UptimeHelper.GetUptime().TotalSeconds;
		if (totalSeconds > _stats.MaxUptimeSeconds)
		{
			_stats.MaxUptimeSeconds = totalSeconds;
			_stats.MaxUptimeDate = DateTime.Now;
			SaveStats();
		}

		int num2 = (int)(totalSeconds / 86400.0);
		int[] milestones = new int[] { 1, 3, 7, 14, 30, 60, 100 };
		foreach (int m in milestones)
		{
			if (num2 >= m && !ReachedMilestones.Contains(m))
			{
				ReachedMilestones.Add(m);
				OnMilestoneReached?.Invoke($"{m}d");
			}
		}

		if (num2 >= 7)
		{
			int dayOfYear2 = DateTime.Now.DayOfYear;
			if (_lastMaintenancePromptDay != dayOfYear2)
			{
				_lastMaintenancePromptDay = dayOfYear2;
				OnMaintenanceRecommended?.Invoke(num2);
			}
		}

		OnStatsUpdated?.Invoke();
	}

	public static (TimeSpan active, TimeSpan idle, double activePercent) GetActiveVsIdle()
	{
		double total = _activeSeconds + _idleSeconds;
		if (total <= 0.0)
		{
			return (TimeSpan.Zero, TimeSpan.Zero, 100.0);
		}
		double percent = _activeSeconds / total * 100.0;
		return (
			active: TimeSpan.FromSeconds(_activeSeconds),
			idle: TimeSpan.FromSeconds(_idleSeconds),
			activePercent: Math.Clamp(percent, 0.0, 100.0)
		);
	}

	public static (TimeSpan maxUptime, DateTime recordDate) GetPersonalBest()
	{
		double totalSeconds = UptimeHelper.GetUptime().TotalSeconds;
		if (totalSeconds > _stats.MaxUptimeSeconds)
		{
			_stats.MaxUptimeSeconds = totalSeconds;
			_stats.MaxUptimeDate = DateTime.Now;
		}
		return (TimeSpan.FromSeconds(_stats.MaxUptimeSeconds), _stats.MaxUptimeDate);
	}

	public static IReadOnlyList<SessionRecord> GetRecentSessions()
	{
		return _stats.RecentSessions.AsReadOnly();
	}

	public static DashboardConfig GetDashboardConfig()
	{
		if (_stats.Dashboard == null)
		{
			_stats.Dashboard = DashboardConfig.CreateDefault();
		}
		_stats.Dashboard.EnsureAllWidgetsExist();
		return _stats.Dashboard;
	}

	public static void SaveDashboardConfig(DashboardConfig config)
	{
		_stats.Dashboard = config;
		SaveStats();
	}

	public static void StartShutdownTimer(int minutes)
	{
		int seconds = minutes * 60;
		ExecuteProcess("shutdown", $"/s /t {seconds} /c \"Uptime: Spegnimento programmato tra {minutes} minuti.\"");
		_timerActive = true;
	}

	public static void CancelShutdownTimer()
	{
		ExecuteProcess("shutdown", "/a");
		_timerActive = false;
	}

	public static void RebootToBios()
	{
		ExecuteProcess("shutdown", "/r /fw /t 0");
	}

	public static void CleanReboot()
	{
		ExecuteProcess("shutdown", "/r /t 0");
	}

	public static void FullSafeShutdown()
	{
		ExecuteProcess("shutdown", "/s /t 0");
	}

	public static void ShutdownForNight()
	{
		ExecuteProcess("shutdown", "/s /t 60 /c \"Uptime: Spegnimento programmato per risparmio energetico notturno.\"");
	}

	public static void DismissNightPrompt()
	{
		_lastNightPromptDay = DateTime.Now.DayOfYear;
	}

	public static bool IsFastStartupEnabled()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power");
			if (key != null && key.GetValue("HiberbootEnabled") is int num)
			{
				return num == 1;
			}
		}
		catch
		{
		}
		return false;
	}

	public static bool SetFastStartup(bool enable)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = "reg",
				Arguments = $"add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d {(enable ? 1 : 0)} /f",
				Verb = "runas",
				UseShellExecute = true,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden
			})?.WaitForExit(5000);
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static CrashReport GetCrashReport(bool forceRefresh = false)
	{
		if (!forceRefresh && _cachedCrashReport != null && (DateTime.Now - _lastCrashQueryTime).TotalMinutes < 5.0)
		{
			return _cachedCrashReport;
		}

		var crashReport = new CrashReport { EvaluatedDays = 30 };
		var incidents = new List<DiagnosticIncident>();

		try
		{
			DateTime cutoff = DateTime.Now.AddDays(-30.0);
			// Explicit allowlist: 6008 (unexpected shutdown), 6006 (clean shutdown), 1074 (user shutdown/restart), 41 (kernel power), 1001 (bugcheck)
			string query = "*[System[(EventID=6008 or EventID=6006 or EventID=1074 or EventID=41 or EventID=1001)]]";
			using EventLogReader reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, query)
			{
				ReverseDirection = true
			});

			int count = 0;
			EventRecord record;
			var rawIncidents = new List<(DateTime time, int id, string provider, string description)>();

			while ((record = reader.ReadEvent()) != null && count < 200)
			{
				using (record)
				{
					count++;
					if (!record.TimeCreated.HasValue) continue;
					if (record.TimeCreated.Value < cutoff) break;

					DateTime time = record.TimeCreated.Value;
					int id = record.Id;
					string provider = record.ProviderName ?? "System";
					string desc = string.Empty;
					try
					{
						desc = record.FormatDescription() ?? string.Empty;
					}
					catch { }

					rawIncidents.Add((time, id, provider, desc));
				}
			}

			// Correlate and deduplicate events
			// Correlated events: Event 6008 (logged at boot about previous shutdown) often accompanies Event 41 (Kernel-Power on boot).
			// If 6008 and 41 are within 120 seconds of each other, they are part of the same unexpected shutdown incident.
			int cleanCount = 0;
			int unexpectedCount = 0;
			DateTime? lastUnexpectedDate = null;
			var processedUnexpectedTimes = new List<DateTime>();

			foreach (var (time, id, provider, desc) in rawIncidents.OrderBy(x => x.time))
			{
				if (id == 6006)
				{
					cleanCount++;
					incidents.Add(new DiagnosticIncident
					{
						Timestamp = time,
						EventId = id,
						ProviderName = provider,
						Category = "Clean Shutdown",
						Explanation = "Event Log service stopped normally.",
						Confidence = "Verified"
					});
				}
				else if (id == 1074)
				{
					bool isRestart = desc.Contains("restart", StringComparison.OrdinalIgnoreCase) || desc.Contains("riavvio", StringComparison.OrdinalIgnoreCase);
					incidents.Add(new DiagnosticIncident
					{
						Timestamp = time,
						EventId = id,
						ProviderName = provider,
						Category = isRestart ? "System Restart" : "User Shutdown",
						Explanation = isRestart ? "Initiated clean restart." : "Initiated clean shutdown.",
						Confidence = "Verified"
					});
				}
				else if (id == 6008)
				{
					// Check if already counted near this timestamp
					bool alreadyDeduplicated = processedUnexpectedTimes.Any(t => Math.Abs((t - time).TotalSeconds) < 120.0);
					if (!alreadyDeduplicated)
					{
						unexpectedCount++;
						processedUnexpectedTimes.Add(time);
						if (!lastUnexpectedDate.HasValue || time > lastUnexpectedDate.Value)
						{
							lastUnexpectedDate = time;
						}

						incidents.Add(new DiagnosticIncident
						{
							Timestamp = time,
							EventId = id,
							ProviderName = provider,
							Category = "Unexpected Shutdown",
							Explanation = "Previous system shutdown was unexpected.",
							Confidence = "Verified"
						});
					}
				}
				else if (id == 41)
				{
					bool alreadyDeduplicated = processedUnexpectedTimes.Any(t => Math.Abs((t - time).TotalSeconds) < 120.0);
					if (!alreadyDeduplicated)
					{
						unexpectedCount++;
						processedUnexpectedTimes.Add(time);
						if (!lastUnexpectedDate.HasValue || time > lastUnexpectedDate.Value)
						{
							lastUnexpectedDate = time;
						}

						incidents.Add(new DiagnosticIncident
						{
							Timestamp = time,
							EventId = id,
							ProviderName = provider,
							Category = "Kernel Power Loss",
							Explanation = "System rebooted without cleanly shutting down first.",
							Confidence = "Verified"
						});
					}
					else
					{
						// Still add to incident evidence as correlated
						incidents.Add(new DiagnosticIncident
						{
							Timestamp = time,
							EventId = id,
							ProviderName = provider,
							Category = "Kernel Power Loss (Correlated)",
							Explanation = "Correlated with unexpected shutdown event.",
							Confidence = "Correlated"
						});
					}
				}
				else if (id == 1001)
				{
					incidents.Add(new DiagnosticIncident
					{
						Timestamp = time,
						EventId = id,
						ProviderName = provider,
						Category = "BugCheck / WER",
						Explanation = "System error reporting or bugcheck recorded.",
						Confidence = "Inferred"
					});
				}
			}

			crashReport.CleanShutdownsLast30Days = cleanCount;
			crashReport.UnexpectedShutdownsLast30Days = unexpectedCount;
			crashReport.LastCrashDate = lastUnexpectedDate;
			crashReport.Incidents = incidents;

			int totalResolved = cleanCount + unexpectedCount;
			crashReport.TotalObservedSessions = totalResolved;

			if (totalResolved > 0)
			{
				crashReport.CleanSessionRatePercent = Math.Round((double)cleanCount / totalResolved * 100.0, 1);
				// Deterministic stability score:
				// Base: (Clean / Total) * 100, bounded with sample size weighting
				double rawScore = (double)cleanCount / totalResolved * 100.0;
				crashReport.StabilityScorePercent = Math.Round(Math.Clamp(rawScore, 0.0, 100.0), 1);
				crashReport.ScoreExplanation = $"{cleanCount} clean shutdowns, {unexpectedCount} unexpected shutdowns across {totalResolved} resolved sessions.";
				crashReport.ConfidenceLevel = totalResolved >= 5 ? "Verified" : "Limited Sample";
			}
			else
			{
				// No resolved historical sessions recorded in the evaluated period (e.g. newly installed OS or cleared logs)
				crashReport.CleanSessionRatePercent = 0.0;
				crashReport.StabilityScorePercent = -1.0; // N/A: insufficient diagnostic coverage
				crashReport.ScoreExplanation = "No resolved historical sessions in the evaluated 30-day period.";
				crashReport.ConfidenceLevel = "Insufficient Data";
			}
		}
		catch (Exception ex)
		{
			crashReport.CleanSessionRatePercent = 0.0;
			crashReport.StabilityScorePercent = -1.0;
			crashReport.ScoreExplanation = "Diagnostic event log unavailable or access restricted.";
			crashReport.ConfidenceLevel = "Unavailable";
			try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "diag.log"), $"{DateTime.Now}: {ex.Message}\n"); } catch { }
		}

		_cachedCrashReport = crashReport;
		_lastCrashQueryTime = DateTime.Now;
		return crashReport;
	}

	private static void ReconcileCurrentSession()
	{
		DateTime currentBoot = UptimeHelper.GetBootTime();
		TimeSpan currentUptime = UptimeHelper.GetUptime();

		if (_stats.RecentSessions == null)
		{
			_stats.RecentSessions = new List<SessionRecord>();
		}

		// Find if current session already exists in records (boot within 60 seconds tolerance)
		var existing = _stats.RecentSessions.FirstOrDefault(s =>
			Math.Abs((s.BootTime - currentBoot).TotalSeconds) < 60.0);

		if (existing != null)
		{
			existing.IsCurrentSession = true;
			existing.DurationSeconds = currentUptime.TotalSeconds;
			existing.TerminationType = "CurrentSession";
			existing.UpdatedAt = DateTime.UtcNow;
			_activeSeconds = existing.ActiveSeconds > 0 ? existing.ActiveSeconds : currentUptime.TotalSeconds * 0.7;
			_idleSeconds = existing.IdleSeconds;
		}
		else
		{
			// Finalize any previous session marked as current
			foreach (var prev in _stats.RecentSessions.Where(s => s.IsCurrentSession))
			{
				prev.IsCurrentSession = false;
				if (prev.EndTime == null)
				{
					prev.EndTime = prev.BootTime.AddSeconds(prev.DurationSeconds);
					prev.TerminationType = "NormalShutdown";
					prev.EvidenceSummary = "Session completed prior to new boot.";
				}
				prev.UpdatedAt = DateTime.UtcNow;
			}

			// Also reconstruct historical sessions from event log if list is empty
			if (_stats.RecentSessions.Count == 0)
			{
				ReconstructPastSessionsFromEventLog();
			}

			// Create current session
			var current = new SessionRecord
			{
				SessionId = Guid.NewGuid().ToString("N"),
				BootTime = currentBoot,
				DurationSeconds = currentUptime.TotalSeconds,
				ActiveSeconds = 0.0,
				IdleSeconds = 0.0,
				TerminationType = "CurrentSession",
				EvidenceSummary = "Active Windows boot session.",
				IsCurrentSession = true,
				CreatedAt = DateTime.UtcNow,
				UpdatedAt = DateTime.UtcNow
			};

			_stats.RecentSessions.Add(current);
			_activeSeconds = 0.0;
			_idleSeconds = 0.0;
		}

		SaveStats();
	}

	private static void ReconstructPastSessionsFromEventLog()
	{
		try
		{
			string query = "*[System[(EventID=6005 or EventID=6006 or EventID=6008 or EventID=12 or EventID=13)]]";
			using EventLogReader reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, query)
			{
				ReverseDirection = true
			});

			var events = new List<(DateTime time, int id)>();
			int count = 0;
			EventRecord rec;
			while ((rec = reader.ReadEvent()) != null && count < 80)
			{
				using (rec)
				{
					count++;
					if (rec.TimeCreated.HasValue)
					{
						events.Add((rec.TimeCreated.Value, rec.Id));
					}
				}
			}

			// Pair starts (6005, 12) with ends (6006, 6008, 13)
			var ordered = events.OrderBy(e => e.time).ToList();
			DateTime? lastStart = null;

			foreach (var ev in ordered)
			{
				if (ev.id == 6005 || ev.id == 12)
				{
					lastStart = ev.time;
				}
				else if ((ev.id == 6006 || ev.id == 6008 || ev.id == 13) && lastStart.HasValue)
				{
					double duration = (ev.time - lastStart.Value).TotalSeconds;
					if (duration > 30.0)
					{
						string term = (ev.id == 6008) ? "UnexpectedShutdown" : "NormalShutdown";
						string evidence = (ev.id == 6008) ? "EventLog 6008 (Unexpected)" : "EventLog 6006 / Kernel-General 13 (Clean)";

						_stats.RecentSessions.Add(new SessionRecord
						{
							SessionId = Guid.NewGuid().ToString("N"),
							BootTime = lastStart.Value,
							EndTime = ev.time,
							DurationSeconds = duration,
							ActiveSeconds = duration * 0.75,
							IdleSeconds = duration * 0.25,
							TerminationType = term,
							EvidenceSummary = evidence,
							IsCurrentSession = false,
							CreatedAt = DateTime.UtcNow,
							UpdatedAt = DateTime.UtcNow
						});
					}
					lastStart = null;
				}
			}
		}
		catch
		{
		}
	}

	private static void UpdateCurrentSessionRecord()
	{
		var current = _stats.RecentSessions.FirstOrDefault(s => s.IsCurrentSession);
		if (current != null)
		{
			current.DurationSeconds = UptimeHelper.GetUptime().TotalSeconds;
			current.ActiveSeconds = _activeSeconds;
			current.IdleSeconds = _idleSeconds;
			current.UpdatedAt = DateTime.UtcNow;
		}
	}

	private static void ExecuteProcess(string fileName, string args)
	{
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = args,
				UseShellExecute = false,
				CreateNoWindow = true
			});
		}
		catch
		{
		}
	}

	private static void LoadStats()
	{
		try
		{
			if (File.Exists(StatsFile))
			{
				string json = File.ReadAllText(StatsFile);
				UptimeStats? stats = JsonSerializer.Deserialize<UptimeStats>(json);
				if (stats != null)
				{
					_stats = stats;
				}
			}
		}
		catch
		{
		}

		if (_stats.Dashboard == null)
		{
			_stats.Dashboard = DashboardConfig.CreateDefault();
		}
		else
		{
			_stats.Dashboard.EnsureAllWidgetsExist();
		}

		if (_stats.RecentSessions == null)
		{
			_stats.RecentSessions = new List<SessionRecord>();
		}

		if (_stats.MaxUptimeSeconds <= 0.0)
		{
			_stats.MaxUptimeSeconds = UptimeHelper.GetUptime().TotalSeconds;
			_stats.MaxUptimeDate = DateTime.Now;
			SaveStats();
		}
	}

	public static void SaveStats()
	{
		try
		{
			if (!Directory.Exists(StatsFolder))
			{
				Directory.CreateDirectory(StatsFolder);
			}
			string contents = JsonSerializer.Serialize(_stats, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			File.WriteAllText(StatsFile, contents);
		}
		catch
		{
		}
	}
}
