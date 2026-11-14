using System.Text.Json;
using Avalonia;
using Uptime.Core;
using Uptime.Infrastructure;

namespace Uptime.Presentation;

internal static class Program
{
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
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                uptimeSeconds = (long)up.TotalSeconds,
                idleSeconds = (long)idle.TotalSeconds,
                health = Health.For(up).ToString(),
                powerPlan = plan.ToString(),
                hasBattery = bat.HasBattery,
                batteryPercent = bat.Percent,
                isCharging = bat.IsCharging
            }, new JsonSerializerOptions { WriteIndented = true }));
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
