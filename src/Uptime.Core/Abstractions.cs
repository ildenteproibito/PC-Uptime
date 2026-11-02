namespace Uptime.Core;

public interface IUptimeProvider { TimeSpan GetUptime(); }
public interface IIdleProvider { TimeSpan GetIdleTime(); }

public interface IEventLogService
{
    /// <summary>Eventi di sistema rilevanti dal momento indicato, in ordine cronologico.</summary>
    IReadOnlyList<LogEvent> ReadEvents(DateTime sinceLocal);
}

public interface IPowerService
{
    void CleanReboot();
    void FullShutdown();
    void RebootToUefi();
    void StartShutdownTimer(int minutes, string comment);
    void CancelShutdownTimer();
    /// <summary>null se il valore non esiste (ibernazione disabilitata) o non e' leggibile.</summary>
    bool? IsFastStartupEnabled();
    /// <summary>Richiede elevazione (UAC). false se rifiutata o fallita.</summary>
    bool SetFastStartup(bool enabled);
}

public interface IStartupService
{
    bool IsEnabled { get; }
    void SetEnabled(bool enabled);
}
