using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class Win32PowerService : IPowerService
{
    private const string PowerKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Power";

    // Reboot is always clean. Full shutdown uses /s (hybrid shutdown requires /hybrid);
    // /full is not a valid shutdown.exe flag and caused shutdown.exe to fail silently.
    // /f forces running apps (including fullscreen windows) to close when the timer expires.
    public void CleanReboot() => RunShutdown("/r /f /t 0");
    public void FullShutdown() => RunShutdown("/s /f /t 0");
    public void RebootToUefi() => RunShutdown("/r /fw /t 0");

    public void StartShutdownTimer(int minutes, string comment)
    {
        var clampedMinutes = Math.Clamp(minutes, 1, 5256000);
        var safeComment = comment.Replace("\"", "").TrimEnd('\\');
        if (safeComment.Length > 512) safeComment = safeComment[..512].TrimEnd('\\');
        RunShutdown($"/s /f /t {clampedMinutes * 60} /c \"{safeComment}\"");
    }

    public void CancelShutdownTimer() => RunShutdown("/a", ignoreFailure: true);

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
            if (!process.WaitForExit(15000)) return false;
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // UAC rejected (1223) or reg.exe not launchable
        }
    }

    private static void RunShutdown(string arguments, bool ignoreFailure = false)
    {
        var shutdownExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shutdown.exe");
        using var process = Process.Start(new ProcessStartInfo(shutdownExe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start shutdown.exe.");
        }

        if (process.WaitForExit(3000) && process.ExitCode != 0 && !ignoreFailure)
        {
            throw new InvalidOperationException($"shutdown.exe exited with code {process.ExitCode}.");
        }
    }
}
