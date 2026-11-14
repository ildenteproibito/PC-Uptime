using System.Reflection;
using System.Text.Json;
using Avalonia;
using Uptime.Core;
using Uptime.Infrastructure;

namespace Uptime.Presentation;

internal static class Program
{
    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record CliJsonStatus(
        long uptimeSeconds,
        long idleSeconds,
        string health,
        string powerPlan,
        bool hasBattery,
        int batteryPercent,
        bool isCharging);

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--restore-workspace", StringComparer.OrdinalIgnoreCase))
        {
            new WindowsWorkspaceRestoreService().RestoreWorkspaceIfPending();
            return;
        }

        if (args.Contains("--json", StringComparer.OrdinalIgnoreCase))
        {
            var up = new Win32UptimeProvider().GetUptime();
            var idle = new Win32IdleProvider().GetIdleTime();
            var bat = new WindowsBatteryService().GetBatteryStatus();
            var plan = new WindowsPowerPlanService().GetActivePlan();
            Console.WriteLine(JsonSerializer.Serialize(new CliJsonStatus(
                (long)up.TotalSeconds,
                (long)idle.TotalSeconds,
                Health.For(up).ToString(),
                plan.ToString(),
                bat.HasBattery,
                bat.Percent,
                bat.IsCharging), new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        // Ensure %LOCALAPPDATA%\Uptime exists on first run before any service/view accesses it
        try
        {
            Directory.CreateDirectory(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Uptime"));
        }
        catch { }

        // Istanza singola (FEAT-08)
        using var mutex = new Mutex(true, "Uptime_SingleInstance_Mutex", out var created);
        if (!created) return;
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Fatal", "Unhandled startup exception.", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
