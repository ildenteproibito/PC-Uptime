using System.Diagnostics.Eventing.Reader;
using System.Text;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Reads boot and shutdown events from the "System" log (12, 13, 41, 1074, 6005, 6006, 6008).</summary>
public sealed class WindowsEventLogService : IEventLogService
{
    private static readonly int[] Ids = { 12, 13, 41, 1074, 6005, 6006, 6008 };

    public IReadOnlyList<LogEvent> ReadEvents(DateTime sinceLocal)
    {
        var ms = (long)Math.Max(1, (DateTime.UtcNow - sinceLocal.ToUniversalTime()).TotalMilliseconds);
        var idFilter = string.Join(" or ", Ids.Select(i => $"EventID={i}"));
        var xpath = $"*[System[({idFilter}) and TimeCreated[timediff(@SystemTime) <= {ms}]]]";

        var result = new List<LogEvent>();
        using var reader = new EventLogReader(new EventLogQuery("System", PathType.LogName, xpath));
        for (var record = reader.ReadEvent(); record != null; record = reader.ReadEvent())
        {
            using (record)
            {
                if (record.TimeCreated is { } time)
                {
                    long? bugcheckCode = null;
                    if (record.Id == 41 && record.Properties.Count > 0 && record.Properties[0]?.Value is not null)
                    {
                        if (long.TryParse(record.Properties[0].Value.ToString(), out var codeVal))
                            bugcheckCode = codeVal;
                    }
                    result.Add(new LogEvent(time, record.Id, record.ProviderName ?? "", bugcheckCode));
                }
            }
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
                        output.AppendLine($"Level: {record.LevelDisplayName ?? record.Level?.ToString() ?? "Unknown"}");
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
