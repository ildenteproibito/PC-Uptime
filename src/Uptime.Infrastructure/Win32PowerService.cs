using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class Win32PowerService : IPowerService
{
    private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";

    // Reboot is always clean (bypasses Fast Startup). Shutdown requires /full,
    // otherwise with Fast Startup enabled Windows performs a hybrid shutdown.
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
                Verb = "runas", // requires UAC elevation
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var process = Process.Start(psi);
            if (process is null) return false;
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // UAC rejected (1223) or reg.exe not launchable
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
