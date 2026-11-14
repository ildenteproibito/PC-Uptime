using System.Diagnostics.Eventing.Reader;
using System.Text;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Reads boot and shutdown events from the "System" log (12, 13, 41, 1074, 6005, 6006, 6008).</summary>
public sealed class WindowsEventLogService : IEventLogService, ISystemIncidentLogService
{
    private static readonly int[] Ids = { 12, 13, 41, 1074, 6005, 6006, 6008 };

    public IReadOnlyList<SystemEventLogItem> ReadRecentCriticalAndErrorEvents(DateTime sinceLocal, int maxItems = 25)
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<SystemEventLogItem>();

        var result = new List<SystemEventLogItem>();
        try
        {
            var ms = (long)Math.Max(1, (DateTime.UtcNow - sinceLocal.ToUniversalTime()).TotalMilliseconds);
            var xpath = $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";
            var query = new EventLogQuery("System", PathType.LogName, xpath)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            while (result.Count < maxItems && reader.ReadEvent() is { } record)
            {
                using (record)
                {
                    if (record.TimeCreated is not { } time) continue;
                    string? level = null;
                    try { level = record.LevelDisplayName; } catch { }
                    if (string.IsNullOrWhiteSpace(level))
                        level = record.Level == 1 ? "Critical" : "Error";

                    var provider = string.IsNullOrWhiteSpace(record.ProviderName) ? "System" : record.ProviderName;
                    var message = "";
                    try
                    {
                        message = record.FormatDescription()?.Trim() ?? "";
                    }
                    catch
                    {
                        message = "";
                    }

                    result.Add(new SystemEventLogItem(time.ToLocalTime(), record.Id, level, provider, message));
                }
            }
        }
        catch
        {
            // Silent fallback if System log is restricted
        }

        return result;
    }

    public IReadOnlyList<LogEvent> ReadEvents(DateTime sinceLocal)
    {
        var result = new List<LogEvent>();
        if (!OperatingSystem.IsWindows()) return result;

        try
        {
            var ms = (long)Math.Max(1, (DateTime.UtcNow - sinceLocal.ToUniversalTime()).TotalMilliseconds);
            var idFilter = string.Join(" or ", Ids.Select(i => $"EventID={i}"));
            var xpath = $"*[System[({idFilter}) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";

            using var reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, xpath));
            for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
            {
                using (record)
                {
                    if (record.TimeCreated is { } time)
                    {
                        long? bugcheckCode = null;
                        try
                        {
                            if (record.Id == 41 && record.Properties.Count > 0 && record.Properties[0]?.Value is not null)
                            {
                                if (long.TryParse(record.Properties[0].Value.ToString(), out var codeVal))
                                    bugcheckCode = codeVal;
                            }
                        }
                        catch { }
                        result.Add(new LogEvent(time, record.Id, record.ProviderName ?? "", bugcheckCode));
                    }
                }
            }
        }
        catch
        {
            // Silent fallback if System log is restricted
        }
        return result.OrderBy(e => e.Time).ToList();
    }

    /// <summary>Reads recent warnings and errors from both Windows channels, retaining full rendered event messages.</summary>
    public static string ReadDiagnosticEvents(DateTime sinceLocal, int maxPerChannel = 300)
    {
        var output = new StringBuilder();
        foreach (var channel in new[] { "System", "Application" })
        {
            output.AppendLine($"===== {channel} =====");
            try
            {
                var ms = (long)Math.Max(1, (DateTime.UtcNow - sinceLocal.ToUniversalTime()).TotalMilliseconds);
                var query = new EventLogQuery(channel, PathType.LogName,
                    $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[timediff(@SystemTime) <= {ms}]]]")
                { ReverseDirection = true };
                using var reader = new EventLogReader(query);
                var count = 0;
                for (var record = reader.ReadEvent(); record is not null && count < maxPerChannel; record = reader.ReadEvent())
                {
                    using (record)
                    {
                        count++;
                        output.AppendLine($"Time: {record.TimeCreated:O}");
                        string? levelDisp = null;
                        try { levelDisp = record.LevelDisplayName; } catch { }
                        output.AppendLine($"Level: {levelDisp ?? record.Level?.ToString() ?? "Unknown"}");
                        output.AppendLine($"Provider: {record.ProviderName ?? "Unknown"}");
                        output.AppendLine($"Event ID: {record.Id}");
                        output.AppendLine("Message:");
                        try { output.AppendLine(record.FormatDescription() ?? "<no rendered message>"); }
                        catch (Exception ex) { output.AppendLine($"<message unavailable: {ex.GetType().Name}: {ex.Message}>"); }
                        output.AppendLine();
                    }
                }
                if (count == 0) output.AppendLine("No warning or error events found in the requested period.");
                else if (count == maxPerChannel) output.AppendLine($"Capped at {maxPerChannel} events.");
            }
            catch (Exception ex)
            {
                output.AppendLine($"Unable to read this channel: {ex.GetType().FullName}: {ex.Message}");
                output.AppendLine(ex.ToString());
            }
            output.AppendLine();
        }
        return output.ToString();
    }
}
