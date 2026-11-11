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

