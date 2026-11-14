using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsStartupAppsService : IStartupAppsService
{
    private static readonly string[] HighImpactKeywords =
    [
        "discord", "steam", "spotify", "chrome", "msedge", "teams", "slack",
        "epicgames", "battle.net", "riotclient", "onedrive", "dropbox", "gog galaxy"
    ];

    private static readonly string[] MediumImpactKeywords =
    [
        "update", "sync", "service", "companion", "razer", "logitech", "corsair", "steelseries", "nvidia"
    ];

    public IReadOnlyList<StartupAppItem> GetStartupApps()
    {
        var list = new List<StartupAppItem>();

        ReadFromRegistry(
            Registry.CurrentUser,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            "HKCU",
            list);
        ReadFromRegistry(
            Registry.LocalMachine,
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            "HKLM",
            list);
        ReadFromRegistry(
            Registry.LocalMachine,
            @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            "HKLM (32-bit)",
            list);

        return list.OrderByDescending(x => x.Impact).ThenBy(x => x.Name).ToList();
    }

    private static void ReadFromRegistry(RegistryKey root, string subKey, string approvedSubKey, string location, List<StartupAppItem> list)
    {
        try
        {
            using var key = root.OpenSubKey(subKey);
            if (key == null) return;

            using var approvedKey = root.OpenSubKey(approvedSubKey);
            foreach (var valueName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName)) continue;
                if (IsDisabledInStartupApproved(approvedKey, valueName)) continue;

                var command = key.GetValue(valueName)?.ToString() ?? string.Empty;
                var impact = ClassifyImpact(valueName, command);
                list.Add(new StartupAppItem(valueName, command, impact, location));
            }
        }
        catch
        {
            // Lettura protetta in caso di restrizioni o chiavi assenti
        }
    }

    private static bool IsDisabledInStartupApproved(RegistryKey? approvedKey, string valueName)
    {
        if (approvedKey?.GetValue(valueName) is not byte[] data || data.Length == 0)
            return false;
        // Windows StartupApproved: 0x02 or 0x06 = enabled; odd values (e.g. 0x03) = disabled by user in Task Manager
        return (data[0] & 0x01) != 0;
    }

    private static StartupImpactLevel ClassifyImpact(string name, string command)
    {
        var text = (name + " " + command).ToLowerInvariant();

        foreach (var high in HighImpactKeywords)
        {
            if (text.Contains(high)) return StartupImpactLevel.High;
        }

        foreach (var med in MediumImpactKeywords)
        {
            if (text.Contains(med)) return StartupImpactLevel.Medium;
        }

        return StartupImpactLevel.Low;
    }
}
