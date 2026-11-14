using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Text.RegularExpressions;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsSleepBlockerService : ISleepBlockerService
{
    public async Task<SleepCheckResult> CheckSleepBlockersAsync()
    {
        var blockers = new List<SleepBlockerItem>();
        var isElevated = true;
        string? lastWake = null;

        try
        {
            var requestsOutput = await RunPowerCfgAsync("/requests");
            if (requestsOutput.Contains("administrator privileges", StringComparison.OrdinalIgnoreCase) ||
                requestsOutput.Contains("privilegi", StringComparison.OrdinalIgnoreCase) ||
                requestsOutput.Contains("администратор", StringComparison.OrdinalIgnoreCase) ||
                requestsOutput.Contains("elevated", StringComparison.OrdinalIgnoreCase))
            {
                isElevated = false;
            }
            else
            {
                ParseRequests(requestsOutput, blockers);
            }
        }
        catch (UnauthorizedAccessException)
        {
            isElevated = false;
        }
        catch
        {
            // Timeout or transient error should not falsely claim missing admin privileges
        }

        // Query last wake reason via powercfg /lastwake and EventLog
        try
        {
            var wakeOutput = await RunPowerCfgAsync("/lastwake");
            if (!string.IsNullOrWhiteSpace(wakeOutput))
            {
                var match = Regex.Match(wakeOutput, @"(?:Wake Source|Origine riattivazione)\s*[:\-]\s*(.+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    lastWake = match.Groups[1].Value.Trim();
                }
            }
        }
        catch
        {
            // Ignore if unavailable
        }

        if (string.IsNullOrWhiteSpace(lastWake))
        {
            lastWake = TryGetLastWakeFromEventLog();
        }

        return new SleepCheckResult(blockers.Count > 0, blockers, lastWake, isElevated);
    }

    private static void ParseRequests(string output, List<SleepBlockerItem> blockers)
    {
        var currentCategory = "";
        using var reader = new StringReader(output);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (line.EndsWith(':'))
            {
                currentCategory = line.TrimEnd(':');
                continue;
            }

            if (string.IsNullOrEmpty(currentCategory)) continue;

            // In all Windows locales, real active requests start with [DRIVER], [PROCESS], [SERVICE], etc.
            // Lines like "None.", "Nessuno.", "Нет.", "Keine." have no brackets and are localized empty markers.
            if (line.StartsWith('['))
            {
                var detail = "";
                var colonIdx = line.IndexOf(']');
                if (colonIdx > 0 && colonIdx < line.Length - 1)
                {
                    detail = line[(colonIdx + 1)..].Trim();
                }
                blockers.Add(new SleepBlockerItem(currentCategory, line, detail));
            }
            else if (blockers.Count > 0 && blockers[^1].Category == currentCategory)
            {
                // Merge multi-line reason text into the preceding blocker instead of creating a duplicate card
                var prev = blockers[^1];
                var mergedDetail = string.IsNullOrWhiteSpace(prev.Detail) ? line : $"{prev.Detail} — {line}";
                blockers[^1] = prev with { Detail = mergedDetail };
            }
        }
    }

    private static string? TryGetLastWakeFromEventLog()
    {
        try
        {
            var query = new EventLogQuery("System", PathType.LogName,
                "*[System[Provider[@Name='Microsoft-Windows-Power-Troubleshooter'] and EventID=1]]")
            { ReverseDirection = true };

            using var reader = new EventLogReader(query);
            using var record = reader.ReadEvent();
            if (record != null)
            {
                var desc = record.FormatDescription();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var match = Regex.Match(desc, @"(?:Wake Source|Origine riattivazione)[:\s]+(.+)", RegexOptions.IgnoreCase);
                    if (match.Success) return match.Groups[1].Value.Trim();
                }
            }
        }
        catch
        {
            // Silent fallback
        }
        return null;
    }

    private static async Task<string> RunPowerCfgAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powercfg.exe",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc is null) return string.Empty;

        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await proc.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { proc.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }
}
