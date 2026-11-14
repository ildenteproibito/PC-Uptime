namespace Uptime.Core;

public interface IUptimeProvider { TimeSpan GetUptime(); }
public interface IIdleProvider { TimeSpan GetIdleTime(); }

public interface IEventLogService
{
    /// <summary>Relevant system events since the specified time, in chronological order.</summary>
    IReadOnlyList<LogEvent> ReadEvents(DateTime sinceLocal);
}

public interface IPowerService
{
    void CleanReboot();
    void FullShutdown();
    void RebootToUefi();
    void StartShutdownTimer(int minutes, string comment);
    void CancelShutdownTimer();
    /// <summary>null if the value does not exist (hibernation disabled) or is unreadable.</summary>
    bool? IsFastStartupEnabled();
    /// <summary>Requires elevation (UAC). false if rejected or failed.</summary>
    bool SetFastStartup(bool enabled);
}

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}

public interface IBatteryService
{
    BatteryInfo GetBatteryStatus();
}

public interface ISleepBlockerService
{
    Task<SleepCheckResult> CheckSleepBlockersAsync();
}

public interface IStartupAppsService
{
    IReadOnlyList<StartupAppItem> GetStartupApps();
}

public interface ISpotifyMediaService
{
    event Action<MediaTrackInfo>? TrackChanged;
    MediaTrackInfo CurrentTrack { get; }
    Task TogglePlayPauseAsync();
    Task SkipNextAsync();
    Task SkipPreviousAsync();
    Task SeekBySecondsAsync(int seconds);
    Task ToggleLoopAsync();
}

public enum PowerPlanKind { Unknown, Balanced, HighPerformance, PowerSaver }

public sealed record AppCrashItem(DateTime Time, string AppName, string Kind, string ModuleName);

public interface IKeepAwakeService
{
    bool IsActive { get; }
    void SetAwake(bool enabled, bool keepDisplayOn = true);
    bool IsProcessRunning(string processName);
}

public interface IPowerPlanService
{
    PowerPlanKind GetActivePlan();
    string GetActivePlanName() => GetActivePlan().ToString();
    bool SetActivePlan(PowerPlanKind plan);
}

public interface IWorkspaceRestoreService
{
    void SaveWorkspaceSnapshot();
    void RestoreWorkspaceIfPending();
}

public sealed record SystemEventLogItem(DateTime Time, int EventId, string Level, string Provider, string Message);

public interface ISystemIncidentLogService
{
    IReadOnlyList<SystemEventLogItem> ReadRecentCriticalAndErrorEvents(DateTime sinceLocal, int maxItems = 25);
}

public interface IAppCrashService
{
    IReadOnlyList<AppCrashItem> ReadRecentAppCrashes(DateTime sinceLocal, int maxItems = 15);
}

public sealed record MemoryStatusInfo(int UsedPercent, double UsedGb, double TotalGb, long AvailableMb);
public sealed record MemoryTrimResult(long FreedMb, int TrimmedProcessCount, MemoryStatusInfo After);

public interface IMemoryService
{
    MemoryStatusInfo GetStatus();
    MemoryTrimResult TrimBackgroundWorkingSets();
}

public sealed record NetworkDropEvent(DateTime Time, string Target, string Reason);
public sealed record NetworkWatchdogSnapshot(
    string Target,
    bool IsOnline,
    long? LastLatencyMs,
    int CheckCount,
    int DropCount,
    IReadOnlyList<NetworkDropEvent> RecentDrops);

public interface INetworkWatchdogService
{
    Task<NetworkWatchdogSnapshot> ProbeAsync(CancellationToken cancellationToken = default);
    NetworkWatchdogSnapshot GetSnapshot();
}

public sealed record CommunityWallpaperItem(
    string Id,
    string Title,
    string Author,
    string Resolution,
    string ThemeTag,
    string ImageUrl,
    int Downloads,
    DateTimeOffset CreatedAt);

public interface ICommunityWallpaperService
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<CommunityWallpaperItem>> GetWallpapersAsync(CancellationToken cancellationToken = default);
    Task<CommunityWallpaperItem> UploadWallpaperAsync(string filePath, string title, string author, string resolution, string themeTag, CancellationToken cancellationToken = default);
    Task<string> DownloadWallpaperToTempAsync(CommunityWallpaperItem item, bool countDownload = true, CancellationToken cancellationToken = default);
    Task<bool> DeleteWallpaperAsync(string wallpaperId, string token, CancellationToken cancellationToken = default);
}


