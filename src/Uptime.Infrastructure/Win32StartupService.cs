using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Avvio con Windows tramite HKCU\...\Run. (Cartella Esecuzione automatica e StartupApproved: blocco successivo.)</summary>
public sealed class Win32StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Uptime";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is not null;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Percorso eseguibile sconosciuto.");
            key.SetValue(ValueName, $"\"{path}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
