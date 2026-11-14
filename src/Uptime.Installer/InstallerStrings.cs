using System;
using System.Collections.Generic;
using System.Globalization;

namespace Uptime.Installer;

internal static class InstallerStrings
{
    private static readonly Dictionary<string, Dictionary<string, string>> Map = new()
    {
        ["it"] = new()
        {
            ["HeaderSubtitle"] = "INSTALLER · v2.0.0",
            ["HeroTitle"] = "Installa Uptime",
            ["HeroDesc"] = "Monitor di sistema in tempo reale, stabilità degli ultimi 30 giorni, OLED Standby e Community Wallpapers. Zero telemetria.",
            ["PathLabel"] = "CARTELLA DI INSTALLAZIONE",
            ["Browse"] = "Sfoglia...",
            ["PickFolder"] = "Seleziona la cartella di installazione",
            ["OptDesktop"] = "Crea collegamento sul Desktop",
            ["OptStartMenu"] = "Aggiungi al Menu Start",
            ["OptStartup"] = "Avvia Uptime all'accesso di Windows",
            ["InstallBtn"] = "Installa Uptime",
            ["CancelBtn"] = "Annulla",
            ["InstallingTitle"] = "Installazione in corso...",
            ["InstallingHint"] = "Preparazione dei file e configurazione dei collegamenti di sistema.",
            ["StepClosing"] = "Chiusura eventuali istanze attive di Uptime...",
            ["StepExtracting"] = "Estrazione di Uptime.exe in corso...",
            ["StepShortcuts"] = "Creazione collegamenti e registrazione in Windows...",
            ["StepDone"] = "Installazione completata!",
            ["DoneTitle"] = "Uptime è pronto",
            ["DoneDesc"] = "Installato correttamente in:",
            ["LaunchAfter"] = "Avvia Uptime subito",
            ["FinishBtn"] = "Fine",
            ["ErrNoPayload"] = "Payload di Uptime.exe non trovato nel pacchetto.",
            ["ErrFailed"] = "Errore durante l'installazione: {0}",
            ["UninstallTitle"] = "Disinstalla Uptime",
            ["UninstallDesc"] = "Rimuoverà il programma, i collegamenti e le voci di sistema mantenendo intatte eventuali impostazioni utente.",
            ["UninstallBtn"] = "Disinstalla",
            ["UninstallingTitle"] = "Disinstallazione in corso...",
            ["UninstallDoneTitle"] = "Uptime rimosso",
            ["UninstallDoneDesc"] = "Tutti i file del programma e i collegamenti sono stati rimossi.",
        },
        ["en"] = new()
        {
            ["HeaderSubtitle"] = "SETUP · v2.0.0",
            ["HeroTitle"] = "Install Uptime",
            ["HeroDesc"] = "Real-time system uptime monitor, 30-day stability history, OLED Standby, and Community Wallpapers. Zero telemetry.",
            ["PathLabel"] = "INSTALL LOCATION",
            ["Browse"] = "Browse...",
            ["PickFolder"] = "Select installation folder",
            ["OptDesktop"] = "Create Desktop shortcut",
            ["OptStartMenu"] = "Add to Start Menu",
            ["OptStartup"] = "Launch Uptime when Windows starts",
            ["InstallBtn"] = "Install Uptime",
            ["CancelBtn"] = "Cancel",
            ["InstallingTitle"] = "Installing Uptime...",
            ["InstallingHint"] = "Extracting application files and configuring Windows shortcuts.",
            ["StepClosing"] = "Closing any running Uptime instances...",
            ["StepExtracting"] = "Extracting Uptime.exe...",
            ["StepShortcuts"] = "Creating shortcuts and registering app...",
            ["StepDone"] = "Installation complete!",
            ["DoneTitle"] = "Uptime is ready",
            ["DoneDesc"] = "Successfully installed to:",
            ["LaunchAfter"] = "Launch Uptime now",
            ["FinishBtn"] = "Finish",
            ["ErrNoPayload"] = "Embedded Uptime.exe payload was not found.",
            ["ErrFailed"] = "Installation failed: {0}",
            ["UninstallTitle"] = "Uninstall Uptime",
            ["UninstallDesc"] = "Removes the application, shortcuts, and Windows registration while keeping your personal settings intact.",
            ["UninstallBtn"] = "Uninstall",
            ["UninstallingTitle"] = "Uninstalling Uptime...",
            ["UninstallDoneTitle"] = "Uptime removed",
            ["UninstallDoneDesc"] = "Application files and shortcuts have been removed.",
        },
        ["ru"] = new()
        {
            ["HeaderSubtitle"] = "УСТАНОВКА · v2.0.0",
            ["HeroTitle"] = "Установить Uptime",
            ["HeroDesc"] = "Монитор времени работы ПК, статистика стабильности за 30 дней, OLED Standby и обои сообщества. Без телеметрии.",
            ["PathLabel"] = "ПАПКА УСТАНОВКИ",
            ["Browse"] = "Обзор...",
            ["PickFolder"] = "Выберите папку установки",
            ["OptDesktop"] = "Создать ярлык на рабочем столе",
            ["OptStartMenu"] = "Добавить в меню «Пуск»",
            ["OptStartup"] = "Запускать Uptime при входе в Windows",
            ["InstallBtn"] = "Установить Uptime",
            ["CancelBtn"] = "Отмена",
            ["InstallingTitle"] = "Установка Uptime...",
            ["InstallingHint"] = "Распаковка файлов и настройка ярлыков Windows.",
            ["StepClosing"] = "Завершение активных процессов Uptime...",
            ["StepExtracting"] = "Распаковка Uptime.exe...",
            ["StepShortcuts"] = "Создание ярлыков и регистрация...",
            ["StepDone"] = "Установка завершена!",
            ["DoneTitle"] = "Uptime готов к работе",
            ["DoneDesc"] = "Успешно установлено в:",
            ["LaunchAfter"] = "Запустить Uptime сейчас",
            ["FinishBtn"] = "Готово",
            ["ErrNoPayload"] = "Встроенный пакет Uptime.exe не найден.",
            ["ErrFailed"] = "Ошибка установки: {0}",
            ["UninstallTitle"] = "Удалить Uptime",
            ["UninstallDesc"] = "Удалит программу, ярлыки и запись в Windows, сохранив пользовательские настройки.",
            ["UninstallBtn"] = "Удалить",
            ["UninstallingTitle"] = "Удаление Uptime...",
            ["UninstallDoneTitle"] = "Uptime удалён",
            ["UninstallDoneDesc"] = "Файлы программы и ярлыки успешно удалены.",
        }
    };

    public static string DefaultLang()
    {
        var two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
        return two is "it" or "ru" ? two : "en";
    }

    public static string Get(string lang, string key)
    {
        if (Map.TryGetValue(lang, out var dict) && dict.TryGetValue(key, out var val)) return val;
        return Map["en"].TryGetValue(key, out var fb) ? fb : key;
    }
}
