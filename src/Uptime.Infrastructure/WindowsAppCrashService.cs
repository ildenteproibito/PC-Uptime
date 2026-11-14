using System.Diagnostics.Eventing.Reader;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsAppCrashService : IAppCrashService
{
    public IReadOnlyList<AppCrashItem> ReadRecentAppCrashes(DateTime sinceLocal, int maxItems = 15)
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<AppCrashItem>();

        var results = new List<AppCrashItem>();
        try
        {
            var sinceUtc = sinceLocal.ToUniversalTime().ToString("o");
            var xpath =
                $"*[System[((Provider[@Name='Application Error'] and EventID=1000) or " +
                $"(Provider[@Name='Application Hang'] and EventID=1002)) and " +
                $"TimeCreated[@SystemTime>='{sinceUtc}']]]";

            var query = new EventLogQuery("Application", PathType.LogName, xpath)
            {
                ReverseDirection = true
            };

            using var reader = new EventLogReader(query);
            while (results.Count < maxItems && reader.ReadEvent() is { } record)
            {
                using (record)
                {
                    if (record.TimeCreated is not { } time) continue;
                    var isHang = record.Id == 1002;
                    var kind = isHang ? "Freeze" : "Crash";

                    var appName = GetPropertyString(record, 0);
                    if (string.IsNullOrWhiteSpace(appName)) continue;

                    // Property 3 in EventID 1000 is typically the faulting module name
                    var moduleName = isHang ? "" : GetPropertyString(record, 3);
                    if (string.Equals(moduleName, "unknown", StringComparison.OrdinalIgnoreCase))
                        moduleName = "";

                    results.Add(new AppCrashItem(time.ToLocalTime(), appName, kind, moduleName));
                }
            }
        }
        catch
        {
            // Silent fallback if Application log is restricted
        }

        return results;
    }

    private static string GetPropertyString(EventRecord record, int index)
    {
        try
        {
            if (record.Properties != null && index < record.Properties.Count)
            {
                return record.Properties[index]?.Value?.ToString()?.Trim() ?? "";
            }
        }
        catch
        {
        }
        return "";
    }
}
