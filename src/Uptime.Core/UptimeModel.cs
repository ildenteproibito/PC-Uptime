namespace Uptime.Core;

public readonly record struct UptimeParts(int Days, int Hours, int Minutes, int Seconds)
{
    public static UptimeParts From(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return new UptimeParts((int)t.TotalDays, t.Hours, t.Minutes, t.Seconds);
    }
}

public enum HealthLevel { Good, Attention, Maintenance }

public static class Health
{
    public const double MaintenanceDays = 7;

    // Verde < 3 giorni, giallo 3-7, arancione > 7
    public static HealthLevel For(TimeSpan uptime) =>
        uptime.TotalDays < 3 ? HealthLevel.Good
        : uptime.TotalDays <= MaintenanceDays ? HealthLevel.Attention
        : HealthLevel.Maintenance;

    public static int DaysUntilMaintenance(TimeSpan uptime) =>
        Math.Max(0, (int)Math.Ceiling(MaintenanceDays - uptime.TotalDays));
}
