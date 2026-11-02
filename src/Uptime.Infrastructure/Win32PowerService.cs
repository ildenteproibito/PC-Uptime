using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class Win32PowerService : IPowerService
{
    private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";

    // Il riavvio e' sempre "pulito" (bypassa Fast Startup). Per lo spegnimento serve /full,
    // altrimenti con Fast Startup attivo Windows esegue uno spegnimento ibrido.
    public void CleanReboot() => RunShutdown("/r /t 0");
    public void FullShutdown() => RunShutdown("/s /full /t 0");
    public void RebootToUefi() => RunShutdown("/r /fw /t 0");

    public void StartShutdownTimer(int minutes, string comment)
    {
        var safeComment = comment.Replace("\"", "");
        RunShutdown($"/s /full /t {minutes * 60} /c \"{safeComment}\"");
    }

    public void CancelShutdownTimer() => RunShutdown("/a");

    public bool? IsFastStartupEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(PowerKey);
            return key?.GetValue("HiberbootEnabled") is int value ? value != 0 : null;
        }
        catch (Exception e) when (e is SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public bool SetFastStartup(bool enabled)
    {
        try
        {
            var psi = new ProcessStartInfo("reg.exe",
                $"add \"HKLM\\{PowerKey}\" /v HiberbootEnabled /t REG_DWORD /d {(enabled ? 1 : 0)} /f")
            {
                UseShellExecute = true,
                Verb = "runas", // richiede UAC
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // UAC rifiutato (1223) o reg.exe non avviabile
        }
    }

    private static void RunShutdown(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("shutdown.exe", arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
