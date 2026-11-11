namespace Uptime.Core;

public sealed record LogEvent(DateTime Time, int Id, string Provider, long? BugcheckCode = null);

public enum SessionOutcome { Clean, Unexpected, Unknown, InProgress }

public sealed record BootSession(DateTime Start, DateTime? End, SessionOutcome Outcome)
{
    public TimeSpan DurationAt(DateTime now) => (End ?? now) - Start;
}

public enum IncidentConfidence { Medium, High }

public sealed record Incident(DateTime Time, IReadOnlyList<int> EventIds, IncidentConfidence Confidence, long? BugcheckCode = null, BsodExplanation? Explanation = null);

public enum DataConfidence { Verified, LimitedSample, InsufficientData }

public sealed record StabilityReport(
    IReadOnlyList<BootSession> Sessions,
    IReadOnlyList<Incident> Incidents,
    int CleanCount,
    int UnexpectedCount,
    double? ScorePercent,
    DataConfidence Confidence,
    TimeSpan Longest);

public static class BsodExplainer
{
    private static readonly Dictionary<long, (string Name, string MeaningIt, string MeaningEn, string AdviceIt, string AdviceEn)> KnownCodes = new()
    {
        [0x0000000A] = (
            "IRQL_NOT_LESS_OR_EQUAL",
            "Un driver o componente kernel ha eseguito un accesso non consentito alla memoria ad un livello IRQL elevato.",
            "A kernel driver attempted an illegal memory access at an elevated IRQL level.",
            "Aggiorna i driver recenti (rete, scheda madre, audio).",
            "Update recent device drivers (network, motherboard chipset, audio)."),

        [0x0000001A] = (
            "MEMORY_MANAGEMENT",
            "Rilevata un'anomalia grave nella gestione della memoria di sistema.",
            "A severe memory management error was detected by the Windows kernel.",
            "Esegui 'Diagnostica memoria Windows' (mdsched.exe) per testare i moduli RAM.",
            "Run Windows Memory Diagnostic (mdsched.exe) to check for faulty RAM sticks."),

        [0x0000001E] = (
            "KMODE_EXCEPTION_NOT_HANDLED",
            "Eccezione kernel non gestita.",
            "An unhandled kernel-mode exception occurred.",
            "Verifica driver installati di recente o conflitti software a basso livello.",
            "Check recently installed drivers or low-level software conflicts."),

        [0x0000003B] = (
            "SYSTEM_SERVICE_EXCEPTION",
            "Errore critico durante l'esecuzione di una routine di sistema o driver video.",
            "A system service routine exception occurred, frequently tied to display drivers.",
            "Aggiorna i driver della GPU ed esegui 'sfc /scannow' da terminale.",
            "Update graphics drivers and run 'sfc /scannow' in an elevated terminal."),

        [0x00000050] = (
            "PAGE_FAULT_IN_NONPAGED_AREA",
            "Richiesta di paging di memoria in un'area non allocata o non valida.",
            "Invalid system memory referenced in a nonpaged memory pool.",
            "Verifica RAM, file di paging di Windows o software di sicurezza in conflitto.",
            "Test RAM modules, verify the Windows paging file, or check conflicting antivirus tools."),

        [0x0000007E] = (
            "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED",
            "Un thread di sistema ha sollevato un'eccezione non intercettata.",
            "A system thread generated an exception which the error handler did not catch.",
            "Aggiorna il BIOS/UEFI e i driver di periferica.",
            "Update motherboard BIOS/UEFI and device drivers."),

        [0x0000009F] = (
            "DRIVER_POWER_STATE_FAILURE",
            "Un driver non ha gestito correttamente la transizione di stato energetico (sleep o sospensione).",
            "A driver failed to handle a power state transition properly (sleep or hibernate).",
            "Aggiorna i driver del chipset e della scheda di rete/Wi-Fi.",
            "Update chipset, Wi-Fi, and power management drivers."),

        [0x000000BE] = (
            "ATTEMPTED_WRITE_TO_READONLY_MEMORY",
            "Un driver ha tentato di scrivere in un segmento di memoria di sola lettura del sistema.",
            "A driver attempted to write to a read-only memory segment of the operating system.",
            "Verifica e aggiorna i driver di dispositivo recenti o software a basso livello aggiunto di recente.",
            "Update recent device drivers or uninstall newly added low-level software."),

        [0x000000D1] = (
            "DRIVER_IRQL_NOT_LESS_OR_EQUAL",
            "Un driver ha causato una violazione di indirizzamento a un livello di interrupt elevato.",
            "A driver tried to access pageable memory at an invalid interrupt request level.",
            "Reinstalla i driver di rete o scheda audio in modalità pulita.",
            "Clean reinstall network adapter or sound card drivers."),

        [0x00000116] = (
            "VIDEO_TDR_FAILURE",
            "Il driver video non ha risposto entro il timeout TDR ed è crashato.",
            "The graphics driver failed to respond within the TDR timeout window.",
            "Reinstalla i driver della scheda video ed escludi problemi termici.",
            "Clean reinstall GPU drivers and check GPU temperatures."),

        [0x00000124] = (
            "WHEA_UNCORRECTABLE_ERROR",
            "Errore hardware irreversibile rilevato da Windows Hardware Error Architecture.",
            "A fatal hardware error was detected by the Windows Hardware Error Architecture.",
            "Controlla le temperature CPU/GPU, disattiva profili overclock instabili e controlla lo stato dei dischi NVMe/SSD.",
            "Check CPU temperatures, disable unstable overclock profiles, and inspect NVMe/SSD drive health."),

        [0x00000133] = (
            "DPC_WATCHDOG_VIOLATION",
            "Una routine DPC ha bloccato il sistema oltre la soglia massima consentita.",
            "The DPC watchdog detected a single long-running deferred procedure call.",
            "Aggiorna firmware SSD e driver controller SATA/NVMe.",
            "Update SSD storage firmware and chipset/SATA controller drivers."),

        [0x00000139] = (
            "KERNEL_SECURITY_CHECK_FAILURE",
            "Rilevata corruzione di un elenco o puntatore critico del kernel.",
            "The kernel detected the corruption of a critical data structure.",
            "Esegui 'DISM /Online /Cleanup-Image /RestoreHealth' e 'sfc /scannow'.",
            "Run 'DISM /Online /Cleanup-Image /RestoreHealth' and 'sfc /scannow' in terminal."),

        [0x000000EF] = (
            "CRITICAL_PROCESS_DIED",
            "Un processo vitale del sistema operativo è terminato in modo imprevisto.",
            "A critical system process terminated unexpectedly.",
            "Verifica integrità disco con 'chkdsk' e scansiona per malware.",
            "Check disk integrity with 'chkdsk' and scan for corrupt system files."),
    };

    public static BsodExplanation Explain(long code, string? language = null)
    {
        var lang = language ?? Localizer.Instance.Language;
        var isIt = string.Equals(lang, "it", StringComparison.OrdinalIgnoreCase);
        var hex = "0x" + code.ToString("X8");

        if (code == 0)
        {
            return new BsodExplanation(
                0,
                "0x00000000",
                "POWER_LOSS_OR_HARD_RESET",
                isIt ? "Arresto improvviso senza BSOD (pressione prolungata del tasto di accensione o interruzione elettrica)."
                     : "Sudden shutdown without BSOD (power button held down or power outage).",
                isIt ? "Evita spegnimenti forzati dal pulsante fisico per prevenire la corruzione dei file su disco."
                     : "Avoid forcing shutdown with the physical button to prevent disk file corruption.");
        }

        if (KnownCodes.TryGetValue(code, out var known))
        {
            return new BsodExplanation(
                code,
                hex,
                known.Name,
                isIt ? known.MeaningIt : known.MeaningEn,
                isIt ? known.AdviceIt : known.AdviceEn);
        }

        return new BsodExplanation(
            code,
            hex,
            "SYSTEM_CRASH",
            isIt ? $"Crash anomalo di sistema (BugCheck {hex})."
                 : $"System crash (BugCheck {hex}).",
            isIt ? "Consulta l'archivio diagnostico o verifica la presenza di aggiornamenti in Windows Update."
                 : "Inspect the diagnostic archive or check for updates in Windows Update.");
    }
}

public static class StabilityAnalyzer
{
    private const string KernelGeneral = "Microsoft-Windows-Kernel-General";
    private const string KernelPower = "Microsoft-Windows-Kernel-Power";
    private const string EventLogProvider = "EventLog";

    public static readonly TimeSpan CrashDedupeWindow = TimeSpan.FromSeconds(120);

    private static bool Is(LogEvent e, int id, string provider) =>
        e.Id == id && string.Equals(e.Provider, provider, StringComparison.OrdinalIgnoreCase);

    public static StabilityReport Analyze(IEnumerable<LogEvent> events, DateTime now)
    {
        var ev = events.OrderBy(e => e.Time).ToList();

        // Boot events: Kernel-General 12; fallback to EventLog 6005.
        var boots = ev.Where(e => Is(e, 12, KernelGeneral)).Select(e => e.Time).ToList();
        if (boots.Count == 0)
            boots = ev.Where(e => Is(e, 6005, EventLogProvider)).Select(e => e.Time).ToList();
        boots = Dedupe(boots, TimeSpan.FromSeconds(60));

        var cleanDown = ev.Where(e => Is(e, 13, KernelGeneral) || Is(e, 6006, EventLogProvider) || Is(e, 1074, "User32")).Select(e => e.Time).ToList();
        var crashEvents = ev.Where(e => Is(e, 6008, EventLogProvider) || Is(e, 41, KernelPower)).ToList();
        var incidents = BuildIncidents(crashEvents);

        var sessions = new List<BootSession>();
        for (var i = 0; i < boots.Count; i++)
        {
            var start = boots[i];
            if (i == boots.Count - 1)
            {
                sessions.Add(new BootSession(start, null, SessionOutcome.InProgress));
                continue;
            }

            var next = boots[i + 1];
            var clean = cleanDown.Where(t => t > start && t <= next).Cast<DateTime?>().LastOrDefault();
            if (clean is { } cleanEnd)
                sessions.Add(new BootSession(start, cleanEnd, SessionOutcome.Clean));
            else if (crashEvents.Any(c => c.Time >= next.AddMinutes(-1) && c.Time <= next.AddMinutes(10)))
                sessions.Add(new BootSession(start, next, SessionOutcome.Unexpected));
            else
                sessions.Add(new BootSession(start, next, SessionOutcome.Unknown));
        }

        var cleanCount = sessions.Count(s => s.Outcome == SessionOutcome.Clean);
        var unexpected = sessions.Count(s => s.Outcome == SessionOutcome.Unexpected);
        var known = cleanCount + unexpected;
        double? score = known > 0 ? cleanCount * 100.0 / known : null;
        var confidence = known >= 10 ? DataConfidence.Verified
            : known >= 3 ? DataConfidence.LimitedSample
            : DataConfidence.InsufficientData;
        var longest = sessions.Count == 0 ? TimeSpan.Zero : sessions.Max(s => s.DurationAt(now));

        return new StabilityReport(sessions, incidents, cleanCount, unexpected, score, confidence, longest);
    }

    // Events 6008 and 41 describe the same occurrence if recorded within 120s: treat as a single incident.
    private static List<Incident> BuildIncidents(List<LogEvent> crashEvents)
    {
        var result = new List<Incident>();
        var group = new List<LogEvent>();

        void Flush()
        {
            if (group.Count == 0) return;
            var ids = group.Select(g => g.Id).Distinct().OrderBy(x => x).ToList();
            var conf = ids.Contains(41) && ids.Contains(6008) ? IncidentConfidence.High : IncidentConfidence.Medium;
            var code = group.FirstOrDefault(e => e.BugcheckCode.HasValue)?.BugcheckCode;
            var explanation = code.HasValue ? BsodExplainer.Explain(code.Value) : null;
            result.Add(new Incident(group[0].Time, ids, conf, code, explanation));
            group.Clear();
        }

        foreach (var e in crashEvents)
        {
            if (group.Count > 0 && e.Time - group[^1].Time > CrashDedupeWindow) Flush();
            group.Add(e);
        }
        Flush();
        return result;
    }

    private static List<DateTime> Dedupe(List<DateTime> times, TimeSpan gap)
    {
        var result = new List<DateTime>();
        foreach (var t in times)
            if (result.Count == 0 || t - result[^1] > gap) result.Add(t);
        return result;
    }
}
