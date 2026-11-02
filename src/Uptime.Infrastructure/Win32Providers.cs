using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Uptime continuo tramite GetTickCount64 (64 bit: nessun overflow a 49 giorni).</summary>
public sealed class Win32UptimeProvider : IUptimeProvider
{
    public TimeSpan GetUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);
}

/// <summary>Tempo di inattività dell'utente tramite user32!GetLastInputInfo.</summary>
public sealed class Win32IdleProvider : IIdleProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!OperatingSystem.IsWindows() || !GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var now = (uint)Environment.TickCount;
        return TimeSpan.FromMilliseconds(unchecked(now - info.dwTime));
    }
}
