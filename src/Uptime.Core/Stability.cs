namespace Uptime.Core;

public sealed record LogEvent(DateTime Time, int Id, string Provider);

public enum SessionOutcome { Clean, Unexpected, Unknown, InProgress }

public sealed record BootSession(DateTime Start, DateTime? End, SessionOutcome Outcome)
{
    public TimeSpan DurationAt(DateTime now) => (End ?? now) - Start;
}

public enum IncidentConfidence { Medium, High }

public sealed record Incident(DateTime Time, IReadOnlyList<int> EventIds, IncidentConfidence Confidence);

public enum DataConfidence { Verified, LimitedSample, InsufficientData }

public sealed record StabilityReport(
    IReadOnlyList<BootSession> Sessions,
    IReadOnlyList<Incident> Incidents,
    int CleanCount,
    int UnexpectedCount,
    double? ScorePercent,
    DataConfidence Confidence,
    TimeSpan Longest);

public static class StabilityAnalyzer
{
    private const string KernelGeneral = "Microsoft-Windows-Kernel-General";
    private const string KernelPower = "Microsoft-Windows-Kernel-Power";
    private const string EventLogProvider = "EventLog";

    public static readonly TimeSpan CrashDedupeWindow = TimeSpan.FromSeconds(120);

    private static bool Is(LogEvent e, int id, string provider) =>
        e.Id == id && string.Equals(e.Provider, provider, StringComparison.OrdinalIgnoreCase);

    public static StabilityReport Analyze(IEnumerable<LogEvent> events, DateTime now)
    {
        var ev = events.OrderBy(e => e.Time).ToList();

        // Avvii: Kernel-General 12; in mancanza, EventLog 6005.
        var boots = ev.Where(e => Is(e, 12, KernelGeneral)).Select(e => e.Time).ToList();
        if (boots.Count == 0)
            boots = ev.Where(e => Is(e, 6005, EventLogProvider)).Select(e => e.Time).ToList();
        boots = Dedupe(boots, TimeSpan.FromSeconds(60));

        var cleanDown = ev.Where(e => Is(e, 13, KernelGeneral) || Is(e, 6006, EventLogProvider) || Is(e, 1074, "User32")).Select(e => e.Time).ToList();
        var crashEvents = ev.Where(e => Is(e, 6008, EventLogProvider) || Is(e, 41, KernelPower)).ToList();
        var incidents = BuildIncidents(crashEvents);

        var sessions = new List<BootSession>();
        for (var i = 0; i < boots.Count; i++)
        {
            var start = boots[i];
            if (i == boots.Count - 1)
            {
                sessions.Add(new BootSession(start, null, SessionOutcome.InProgress));
                continue;
            }

            var next = boots[i + 1];
            var clean = cleanDown.Where(t => t > start && t <= next).Cast<DateTime?>().LastOrDefault();
            if (clean is { } cleanEnd)
                sessions.Add(new BootSession(start, cleanEnd, SessionOutcome.Clean));
            else if (crashEvents.Any(c => c.Time >= next.AddMinutes(-1) && c.Time <= next.AddMinutes(10)))
                sessions.Add(new BootSession(start, next, SessionOutcome.Unexpected));
            else
                sessions.Add(new BootSession(start, next, SessionOutcome.Unknown));
        }

        var cleanCount = sessions.Count(s => s.Outcome == SessionOutcome.Clean);
        var unexpected = sessions.Count(s => s.Outcome == SessionOutcome.Unexpected);
        var known = cleanCount + unexpected;
        double? score = known > 0 ? cleanCount * 100.0 / known : null;
        var confidence = known >= 10 ? DataConfidence.Verified
            : known >= 3 ? DataConfidence.LimitedSample
            : DataConfidence.InsufficientData;
        var longest = sessions.Count == 0 ? TimeSpan.Zero : sessions.Max(s => s.DurationAt(now));

        return new StabilityReport(sessions, incidents, cleanCount, unexpected, score, confidence, longest);
    }

    // 6008 e 41 descrivono lo stesso evento se registrati entro 120 s: un solo incidente.
    private static List<Incident> BuildIncidents(List<LogEvent> crashEvents)
    {
        var result = new List<Incident>();
        var group = new List<LogEvent>();

        void Flush()
        {
            if (group.Count == 0) return;
            var ids = group.Select(g => g.Id).Distinct().OrderBy(x => x).ToList();
            var conf = ids.Contains(41) && ids.Contains(6008) ? IncidentConfidence.High : IncidentConfidence.Medium;
            result.Add(new Incident(group[0].Time, ids, conf));
            group.Clear();
        }

        foreach (var e in crashEvents)
        {
            if (group.Count > 0 && e.Time - group[^1].Time > CrashDedupeWindow) Flush();
            group.Add(e);
        }
        Flush();
        return result;
    }

    private static List<DateTime> Dedupe(List<DateTime> times, TimeSpan gap)
    {
        var result = new List<DateTime>();
        foreach (var t in times)
            if (result.Count == 0 || t - result[^1] > gap) result.Add(t);
        return result;
    }
}
