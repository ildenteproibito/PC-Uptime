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

        ReadFromRegistry(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU", list);
        ReadFromRegistry(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM", list);

        return list.OrderByDescending(x => x.Impact).ThenBy(x => x.Name).ToList();
    }

    private static void ReadFromRegistry(RegistryKey root, string subKey, string location, List<StartupAppItem> list)
    {
        try
        {
            using var key = root.OpenSubKey(subKey);
            if (key == null) return;

            foreach (var valueName in key.GetValueNames())
            {
                if (string.IsNullOrWhiteSpace(valueName)) continue;
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
