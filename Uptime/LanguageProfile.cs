using System;
using System.Globalization;

namespace AwayUptime;

public record LanguageProfile(
	string Id,
	string Code,
	string Name,
	string EnglishName,
	string Flag,
	CultureInfo Culture,
	string LastBootHeader,
	string StartupOption,
	string CopyButton,
	string CopiedFeedback,
	string ShowMenu,
	string CopyMenu,
	string ExitMenu,
	string AlreadyRunningNotice,
	Func<int, string> PluralDays,
	Func<int, string> PluralHours,
	Func<int, string> PluralMinutes,
	Func<int, string> PluralSeconds,
	string DaysSymbol = "d",
	string HoursSymbol = "h",
	string MinutesSymbol = "m",
	string SecondsSymbol = "s",
	string TabOverview = "Panoramica",
	string TabRecords = "Record",
	string TabActions = "Azioni",
	string BootDurationHeader = "TEMPO DI AVVIO",
	string ActiveLabel = "Attivo",
	string IdleLabel = "Inattivo",
	string PersonalBestHeader = "RECORD DI STABILITÀ",
	string MilestonesHeader = "TRAGUARDI RAGGIUNTI",
	string PowerWarning = "ATTENZIONE: Queste azioni riavviano o spengono il PC.",
	string SafeShutdownTitle = "Spegnimento Completo e Sicuro",
	string SafeShutdownDesc = "Spegne il PC azzerando kernel e cache, senza avvio rapido ibrido.",
	string BtnSafeShutdown = "Spegni Ora",
	string SafeShutdownConfirmMessage = "Il PC verrà spento completamente (senza ibernazione ibrida). Salva tutti i tuoi file prima di procedere.",
	string RebootBiosTitle = "Riavvia nel BIOS / UEFI",
	string RebootBiosDesc = "Riavvia il computer ed entra direttamente nel firmware UEFI.",
	string CleanRebootTitle = "Riavvio Pulito Immediato",
	string CleanRebootDesc = "Riavvio puro senza ibernazione per azzerare la cache dei driver.",
	string TimerTitle = "Timer Spegnimento",
	string CancelTimer = "Annulla Timer",
	string ConfirmTitle = "Conferma Riavvio BIOS",
	string ConfirmMessage = "Il computer verrà riavviato immediatamente nel BIOS / UEFI. Assicurati di aver salvato il tuo lavoro.",
	string ConfirmProceed = "Riavvia nel BIOS",
	string ConfirmCancel = "Annulla",
	string BtnRebootBios = "Riavvia BIOS",
	string BtnCleanReboot = "Riavvia Ora",
	string CleanRebootConfirmMessage = "Il computer verrà riavviato immediatamente in modalità pulita. Salva tutti i tuoi file prima di procedere.",
	string HealthAdvisorHeader = "CONSIGLIO SALUTE PC",
	string HealthStatusGreen = "Tutto perfetto! Il tuo PC è scattante e in ottima salute.",
	string HealthStatusYellow = "Stato buono. Se noti rallentamenti, un riavvio farà bene.",
	string HealthStatusOrange = "Acceso da oltre 7 giorni. Un riavvio consigliato libererà memoria e cache.",
	string BtnRefreshPc = "Rinfresca",
	string RefreshPcSuccess = "Rinfrescato! (+{0} MB)",
	string NightPromptTitle = "Buonanotte da Uptime \ud83c\udf19",
	string NightPromptMessage = "Il computer è inattivo da oltre 30 minuti. Vuoi spegnerlo per risparmiare energia?",
	string BtnNightShutdown = "Spegni PC",
	string BtnNightStayOn = "Lascialo Acceso",
	string FastStartupTitle = "Avvio Rapido (Fast Startup)",
	string FastStartupDescActive = "Attivo: Il kernel viene ibernato allo spegnimento per velocizzare l'avvio, ma non azzera l'uptime né la memoria.",
	string FastStartupDescDisabled = "Disattivato: Spegnimento pulito al 100%. L'uptime e lo stato dei driver vengono resettati ad ogni arresto.",
	string FastStartupStatusActive = "ATTIVO (Ibrido)",
	string FastStartupStatusDisabled = "DISATTIVATO (Pulito)",
	string BtnDisableFastStartup = "Disattiva",
	string BtnEnableFastStartup = "Attiva",
	string StabilityHeader = "AFFIDABILITÀ & CRASH DI SISTEMA",
	string StabilityRateLabel = "Indice di Stabilità",
	string CrashesDetectedFormat = "{0} arresti anomali (ultimi 30 gg)",
	string NoCrashesMessage = "0 crash rilevati • Sistema Perfetto",
	string LastCrashFormat = "Ultimo crash: {0}",
	string NoRecentCrashMessage = "Nessun arresto anomalo recente",
	string ShareCardHeader = "CONDIVIDI BADGE STABILITÀ",
	string BadgeSubBrand = "BADGE DI STABILITÀ SISTEMA",
	string BadgeVerified = "● REPORT WINDOWS VERIFICATO",
	string BadgeActiveSession = "SESSIONE ATTIVA",
	string BtnShareBadge = "Condividi Card",
	string BtnShareBadgeFull = "Copia Card per Reddit / Discord",
	string BadgeCopiedToast = "Card copiata negli appunti!",
	string BadgeSubtext = "Incollala su Reddit o Discord (Ctrl+V)",
	string HotkeyHint = "Scorciatoia Rapida: Win + Alt + U",
	string MaintenanceNotificationTitle = "Uptime - Manutenzione Consigliata",
	string MaintenanceNotificationMsg = "Il PC è attivo da oltre {0} giorni consecutivi. Si consiglia un riavvio pulito per liberare memoria e ripristinare le prestazioni ottimali.",
	string NotAvailable = "N/D",
	string CustomizeDashboard = "Personalizza Dashboard",
	string ResetDefault = "Ripristina Predefiniti",
	string PresetDefault = "Predefinito",
	string PresetMinimal = "Minimale",
	string PresetReliability = "Affidabilità",
	string PresetCustom = "Personalizzato",
	string EmptyDashboardNotice = "Tutti i widget del dashboard sono nascosti.",
	string Filter24h = "24 Ore",
	string Filter7d = "7 Giorni",
	string Filter30d = "30 Giorni",
	string FilterAll = "Tutti",
	string SessionTimelineHeader = "CRONOLOGIA SESSIONI",
	string CleanRateLabel = "Tasso Pulito",
	string AvgDurationLabel = "Durata Media",
	string ObservedSessionsLabel = "Sessioni Osservate",
	string ExportJsonBtn = "Esporta JSON",
	string ExportCsvBtn = "Esporta CSV",
	string ExportSuccess = "Esportazione completata!",
	string RefreshDiagnosticsBtn = "Aggiorna Diagnostica",
	string DiagnosticsRefreshed = "Diagnostica aggiornata!",
	string CopySummaryBtn = "Copia Report",
	string WidgetUptime = "Tempo di Attività (Uptime)",
	string WidgetBootTime = "Ultimo Avvio & Durata",
	string WidgetActiveIdle = "Tempo Attivo / Inattivo",
	string WidgetHealthAdvisor = "Consiglio Salute PC",
	string WidgetReliabilitySummary = "Riepilogo Affidabilità",
	string WidgetCrashesSummary = "Arresti Anomali Recenti",
	string WidgetCurrentSession = "Sessione Corrente",
	string WidgetSessionPreview = "Anteprima Sessioni",
	string WidgetStabilityScore = "Indice di Stabilità",
	string WidgetLongestSession = "Record Sessione Più Lunga",
	string WidgetObservedSessions = "Sessioni Totali Osservate",
	string WidgetSystemInfo = "Informazioni di Sistema")
{
	public string RefreshDiagnosticsHeader { get; init; } = "Aggiorna Diagnostica & Affidabilità";
	public string RefreshDiagnosticsDesc { get; init; } = "Rianalizza il registro eventi di Windows per arresti anomali e affidabilità.";
	public string ExportDataHeader { get; init; } = "Esporta Dati Sessioni & Affidabilità";
	public string ExportDataDesc { get; init; } = "Salva report completo in formato strutturato JSON o tabellare CSV.";
	public string CopySummaryHeader { get; init; } = "Copia Riepilogo Affidabilità";
	public string CopySummaryDesc { get; init; } = "Copia negli appunti statistiche e report di stabilità formattati.";
	public string BtnEditDashboard { get; init; } = "Modifica";
	public string BtnCardLabel { get; init; } = "Card";
	public string ToolTipCustomizeDashboard { get; init; } = "Personalizza Dashboard";
	public string ToolTipPinHud { get; init; } = "Fissa Mini-Widget sul Desktop (HUD)";
	public string ToolTipUnpinHud { get; init; } = "Mini-Widget Desktop (HUD) ATTIVO - Clicca per staccare";
	public string ToolTipQuickShare { get; init; } = "Copia Card Statistiche (Reddit/Discord)";
	public string ToolTipClose { get; init; } = "Chiudi";
	public string ToolTipHudDrag { get; init; } = "Doppio clic per aprire Uptime • Trascina per riposizionare";
	public string ToolTipHudOpen { get; init; } = "Apri Uptime";
	public string ToolTipHudClose { get; init; } = "Chiudi Mini-HUD";
	public string TimelineActiveSessionBadge { get; init; } = "ATTIVA";
	public string TimelineInProgressBadge { get; init; } = "In Corso";
	public string TimelineDurationLabel { get; init; } = "Durata";
	public string TimelineNormalShutdownBadge { get; init; } = "Pulito";
	public string TimelineRestartBadge { get; init; } = "Riavvio";
	public string TimelineUnexpectedShutdownBadge { get; init; } = "Anomalo";
	public string TimelineCompletedBadge { get; init; } = "Concluso";
	public string TimelineNoSessionsNotice { get; init; } = "Nessuna sessione registrata nel periodo selezionato.";
	public string TimelineEvidenceActiveBoot { get; init; } = "Sessione di avvio di Windows attiva.";
	public string TimelineEvidenceCompletedPrior { get; init; } = "Sessione completata prima del nuovo avvio.";
	public string StartedLabel { get; init; } = "Avviata";

	public (string primary, string secondary) FormatDisplay(TimeSpan uptime)
	{
		int days = (int)uptime.TotalDays;
		int hours = uptime.Hours;
		int minutes = uptime.Minutes;
		int seconds = uptime.Seconds;
		if (days > 0)
		{
			return (PluralDays(days) + ", " + PluralHours(hours), PluralMinutes(minutes) + ", " + PluralSeconds(seconds));
		}
		if (hours > 0)
		{
			return (PluralHours(hours) + ", " + PluralMinutes(minutes), PluralSeconds(seconds));
		}
		return (PluralMinutes(minutes), PluralSeconds(seconds));
	}

	public string FormatTooltip(TimeSpan uptime)
	{
		int days = (int)uptime.TotalDays;
		if (days <= 0)
		{
			return $"Uptime: {uptime.Hours:D2}{HoursSymbol} {uptime.Minutes:D2}{MinutesSymbol} {uptime.Seconds:D2}{SecondsSymbol}";
		}
		return $"Uptime: {days}{DaysSymbol} {uptime.Hours:D2}{HoursSymbol} {uptime.Minutes:D2}{MinutesSymbol} {uptime.Seconds:D2}{SecondsSymbol}";
	}

	public string FormatFull(TimeSpan uptime, DateTime bootTime)
	{
		var (primary, secondary) = FormatDisplay(uptime);
		string bootStr = bootTime.ToString("g", Culture);
		return $"Uptime: {primary}, {secondary} ({LastBootHeader.ToLower(Culture)}: {bootStr})";
	}

	public string GetWidgetTitle(string widgetId) => widgetId switch
	{
		"uptime" => WidgetUptime,
		"boot_time" => WidgetBootTime,
		"active_idle" => WidgetActiveIdle,
		"health_advisor" => WidgetHealthAdvisor,
		"reliability_summary" => WidgetReliabilitySummary,
		"crashes_summary" => WidgetCrashesSummary,
		"current_session" => WidgetCurrentSession,
		"session_history_preview" => WidgetSessionPreview,
		"stability_score" => WidgetStabilityScore,
		"longest_session" => WidgetLongestSession,
		"observed_sessions" => WidgetObservedSessions,
		"system_info" => WidgetSystemInfo,
		_ => widgetId
	};

	public string GetPresetTitle(string presetName) => presetName switch
	{
		DashboardConfig.PresetDefault => PresetDefault,
		DashboardConfig.PresetMinimal => PresetMinimal,
		DashboardConfig.PresetReliability => PresetReliability,
		DashboardConfig.PresetCustom => PresetCustom,
		_ => presetName
	};
}
