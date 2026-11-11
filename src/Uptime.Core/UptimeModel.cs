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

public sealed record BatteryInfo(
    bool HasBattery,
    int Percent,
    bool IsCharging,
    int? HealthPercent,
    int? DesignCapacityMWh,
    int? FullChargeCapacityMWh,
    int? CycleCount,
    string PowerStatusText);

public enum StartupImpactLevel { Low, Medium, High }

public sealed record StartupAppItem(
    string Name,
    string Command,
    StartupImpactLevel Impact,
    string Location);

public sealed record SleepBlockerItem(
    string Category,
    string Name,
    string Detail);

public sealed record SleepCheckResult(
    bool HasBlockers,
    IReadOnlyList<SleepBlockerItem> Blockers,
    string? LastWakeReason,
    bool IsElevated);

public sealed record MediaTrackInfo(
    bool HasTrack,
    string Title,
    string Artist,
    string Album,
    bool IsPlaying,
    string SourceApp,
    byte[]? ThumbnailBytes,
    TimeSpan Position = default,
    TimeSpan Duration = default,
    bool IsLooping = false);

public sealed record BsodExplanation(
    long Code,
    string HexCode,
    string Name,
    string Meaning,
    string Advice);

