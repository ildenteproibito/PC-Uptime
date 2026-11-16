using System.Text.RegularExpressions;

namespace Uptime.Core;

/// <summary>
/// Translates Windows Event Viewer levels and common System event messages into the active app language (it, en, ru).
/// </summary>
public static partial class EventLogLocalizer
{
    public static bool IsCriticalLevel(string? rawLevel)
    {
        if (string.IsNullOrWhiteSpace(rawLevel)) return false;
        return rawLevel.Contains("crit", StringComparison.OrdinalIgnoreCase)
            || rawLevel.Contains("крит", StringComparison.OrdinalIgnoreCase);
    }

    public static string LocalizeLevel(string? rawLevel, string lang)
    {
        var isCrit = IsCriticalLevel(rawLevel);
        return lang switch
        {
            "it" => isCrit ? "Critico" : "Errore",
            "ru" => isCrit ? "Критическая" : "Ошибка",
            _ => isCrit ? "Critical" : "Error"
        };
    }

    public static string LocalizeMessage(int eventId, string provider, string? rawMessage, string lang)
    {
        var msg = (rawMessage ?? string.Empty).Trim();

        // Service Control Manager (7000, 7001, 7009, 7023, 7031, 7034)
        if (eventId is 7000 or 7001 or 7009 or 7023 or 7031 or 7034)
        {
            var translatedScm = TryTranslateScm(eventId, msg, lang);
            if (!string.IsNullOrEmpty(translatedScm))
                return translatedScm;
        }

        // Common System Event IDs
        var known = eventId switch
        {
            41 => lang switch
            {
                "it" => "Riavvio del sistema senza un arresto regolare precedente (perdita di alimentazione, crash o blocco).",
                "ru" => "Система перезагрузилась без корректного завершения работы (сбой питания, зависание или критическая ошибка).",
                _ => "The system has rebooted without cleanly shutting down first (power loss, crash, or hang)."
            },
            6008 => lang switch
            {
                "it" => "Arresto imprevisto del sistema rilevato all'avvio precedente.",
                "ru" => "Обнаружено неожиданное завершение работы системы.",
                _ => "The previous system shutdown was unexpected."
            },
            1001 when (provider ?? "").Contains("BugCheck", StringComparison.OrdinalIgnoreCase) => lang switch
            {
                "it" => "Il computer è stato riavviato in seguito a un controllo di bug (schermata blu / BSOD).",
                "ru" => "Компьютер был перезагружен после критической ошибки (BSOD).",
                _ => "The computer has rebooted from a bugcheck (BSOD)."
            },
            10010 => lang switch
            {
                "it" => "Un server DCOM non si è registrato con DCOM entro il timeout richiesto.",
                "ru" => "Регистрация DCOM-сервера не выполнена в отведенное время ожидания.",
                _ => "A DCOM server did not register with DCOM within the required timeout."
            },
            10016 => lang switch
            {
                "it" => "Autorizzazioni DCOM specifiche dell'applicazione non concesse per l'attivazione locale.",
                "ru" => "Параметры разрешений DCOM не предоставляют локальное разрешение на активацию.",
                _ => "Application-specific DCOM permission settings do not grant local activation permission."
            },
            1014 => lang switch
            {
                "it" => "Timeout nella risoluzione DNS dei nomi; nessun server DNS configurato ha risposto.",
                "ru" => "Превышено время ожидания разрешения имен DNS; DNS-серверы не ответили.",
                _ => "Name resolution timed out after none of the configured DNS servers responded."
            },
            _ => null
        };

        if (known is not null)
            return known;

        return string.IsNullOrWhiteSpace(msg) ? provider : msg;
    }

    private static string? TryTranslateScm(int eventId, string msg, string lang)
    {
        if (eventId == 7000)
        {
            var m = Scm7000EnRegex().Match(msg);
            if (!m.Success) m = Scm7000ItRegex().Match(msg);
            if (!m.Success) m = Scm7000RuRegex().Match(msg);
            if (m.Success)
            {
                var svc = m.Groups["svc"].Value.Trim();
                var reason = TranslateCommonReason(m.Groups["reason"].Value.Trim(), lang);
                return lang switch
                {
                    "it" => $"Il servizio {svc} non è riuscito ad avviarsi a causa del seguente errore:\n{reason}",
                    "ru" => $"Сбой при запуске службы {svc} из-за следующей ошибки:\n{reason}",
                    _ => $"The {svc} service failed to start due to the following error:\n{reason}"
                };
            }
        }

        if (eventId == 7009)
        {
            var m = Scm7009EnRegex().Match(msg);
            if (!m.Success) m = Scm7009ItRegex().Match(msg);
            if (!m.Success) m = Scm7009RuRegex().Match(msg);
            if (m.Success)
            {
                var msVal = m.Groups["ms"].Value;
                var svc = m.Groups["svc"].Value.Trim();
                return lang switch
                {
                    "it" => $"Timeout ({msVal} millisecondi) durante l'attesa della connessione del servizio {svc}.",
                    "ru" => $"Таймаут ({msVal} мс) ожидания подключения службы {svc}.",
                    _ => $"A timeout was reached ({msVal} milliseconds) while waiting for the {svc} service to connect."
                };
            }
        }

        if (eventId == 7023)
        {
            var m = Scm7023EnRegex().Match(msg);
            if (!m.Success) m = Scm7023ItRegex().Match(msg);
            if (!m.Success) m = Scm7023RuRegex().Match(msg);
            if (m.Success)
            {
                var svc = m.Groups["svc"].Value.Trim();
                var reason = TranslateCommonReason(m.Groups["reason"].Value.Trim(), lang);
                return lang switch
                {
                    "it" => $"Il servizio {svc} è terminato con il seguente errore:\n{reason}",
                    "ru" => $"Служба {svc} завершена с ошибкой:\n{reason}",
                    _ => $"The {svc} service terminated with the following error:\n{reason}"
                };
            }
        }

        if (eventId is 7031 or 7034)
        {
            var m = ScmUnexpectedEnRegex().Match(msg);
            if (!m.Success) m = ScmUnexpectedItRegex().Match(msg);
            if (!m.Success) m = ScmUnexpectedRuRegex().Match(msg);
            if (m.Success)
            {
                var svc = m.Groups["svc"].Value.Trim();
                return lang switch
                {
                    "it" => $"Il servizio {svc} è terminato in modo imprevisto.",
                    "ru" => $"Служба {svc} неожиданно завершила работу.",
                    _ => $"The {svc} service terminated unexpectedly."
                };
            }
        }

        if (eventId == 7001)
        {
            var m = Scm7001EnRegex().Match(msg);
            if (!m.Success) m = Scm7001ItRegex().Match(msg);
            if (!m.Success) m = Scm7001RuRegex().Match(msg);
            if (m.Success)
            {
                var svc = m.Groups["svc"].Value.Trim();
                var dep = m.Groups["dep"].Value.Trim();
                var reason = TranslateCommonReason(m.Groups["reason"].Value.Trim(), lang);
                return lang switch
                {
                    "it" => $"Il servizio {svc} dipende dal servizio {dep}, che non è riuscito ad avviarsi:\n{reason}",
                    "ru" => $"Служба {svc} зависит от службы {dep}, которую не удалось запустить:\n{reason}",
                    _ => $"The {svc} service depends on the {dep} service which failed to start:\n{reason}"
                };
            }
        }

        return null;
    }

    private static string TranslateCommonReason(string reason, string lang)
    {
        if (string.IsNullOrWhiteSpace(reason)) return reason;

        if (reason.Contains("timely fashion", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("tempo utile", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("своевременно", StringComparison.OrdinalIgnoreCase))
        {
            return lang switch
            {
                "it" => "Il servizio non ha risposto alla richiesta di avvio o controllo in tempo utile.",
                "ru" => "Служба не ответила на запрос своевременно.",
                _ => "The service did not respond to the start or control request in a timely fashion."
            };
        }

        if (reason.Contains("cannot find the file specified", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("trovare il file specificato", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("найти указанный файл", StringComparison.OrdinalIgnoreCase))
        {
            return lang switch
            {
                "it" => "Impossibile trovare il file specificato.",
                "ru" => "Не удается найти указанный файл.",
                _ => "The system cannot find the file specified."
            };
        }

        if (reason.Contains("cannot find the path specified", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("trovare il percorso specificato", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("найти указанный путь", StringComparison.OrdinalIgnoreCase))
        {
            return lang switch
            {
                "it" => "Impossibile trovare il percorso specificato.",
                "ru" => "Не удается найти указанный путь.",
                _ => "The system cannot find the path specified."
            };
        }

        if (reason.Contains("Access is denied", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("Accesso negato", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("Отказано в доступе", StringComparison.OrdinalIgnoreCase))
        {
            return lang switch
            {
                "it" => "Accesso negato.",
                "ru" => "Отказано в доступе.",
                _ => "Access is denied."
            };
        }

        if (reason.Contains("dependency service or group failed", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("servizio o il gruppo di dipendenza", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("дочерней службе", StringComparison.OrdinalIgnoreCase))
        {
            return lang switch
            {
                "it" => "Impossibile avviare il servizio o il gruppo di dipendenza.",
                "ru" => "Не удалось запустить дочернюю службу.",
                _ => "The dependency service or group failed to start."
            };
        }

        return reason;
    }

    [GeneratedRegex(@"^The (?<svc>.+?) service failed to start due to the following error:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7000EnRegex();

    [GeneratedRegex(@"^Il servizio (?<svc>.+?) non è riuscito ad avviarsi a causa del seguente errore:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7000ItRegex();

    [GeneratedRegex(@"^Сбой при запуске службы (?<svc>.+?) из-за следующей ошибки:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7000RuRegex();

    [GeneratedRegex(@"^A timeout was reached \((?<ms>\d+) milliseconds\) while waiting for the (?<svc>.+?) service to connect\.?$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7009EnRegex();

    [GeneratedRegex(@"^Timeout \((?<ms>\d+) millisecondi\) durante l'attesa della connessione del servizio (?<svc>.+?)\.?$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7009ItRegex();

    [GeneratedRegex(@"^Таймаут \((?<ms>\d+) мс\) ожидания подключения службы (?<svc>.+?)\.?$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7009RuRegex();

    [GeneratedRegex(@"^The (?<svc>.+?) service terminated with the following error:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7023EnRegex();

    [GeneratedRegex(@"^Il servizio (?<svc>.+?) è terminato con il seguente errore:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7023ItRegex();

    [GeneratedRegex(@"^Служба ""?(?<svc>.+?)""? была завершена из-за следующей ошибки:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7023RuRegex();

    [GeneratedRegex(@"^The (?<svc>.+?) service terminated unexpectedly", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScmUnexpectedEnRegex();

    [GeneratedRegex(@"^Il servizio (?<svc>.+?) è terminato in modo imprevisto", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScmUnexpectedItRegex();

    [GeneratedRegex(@"^Служба ""?(?<svc>.+?)""? неожиданно завершена", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ScmUnexpectedRuRegex();

    [GeneratedRegex(@"^The (?<svc>.+?) service depends on the (?<dep>.+?) service which failed to start because of the following error:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7001EnRegex();

    [GeneratedRegex(@"^Il servizio (?<svc>.+?) dipende dal servizio (?<dep>.+?) che non è riuscito ad avviarsi a causa del seguente errore:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7001ItRegex();

    [GeneratedRegex(@"^Служба ""?(?<svc>.+?)""? зависит от службы ""?(?<dep>.+?)""?, которую не удалось запустить из-за следующей ошибки:\s*(?<reason>.+)$", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Scm7001RuRegex();
}
