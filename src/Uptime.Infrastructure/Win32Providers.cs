using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Uptime continuo tramite GetTickCount64 (64 bit: nessun overflow a 49 giorni).</summary>
public sealed class Win32UptimeProvider : IUptimeProvider
{
    public TimeSpan GetUptime() => TimeSpan.FromMilliseconds(Environment.TickCount64);
}

/// <summary>Tempo di inattività dell'utente tramite user32!GetLastInputInfo con monitoraggio posizione cursore e deadzone anti-jitter.</summary>
public sealed class Win32IdleProvider : IIdleProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    private readonly object _gate = new();
    private POINT _lastPoint;
    private bool _hasLastPoint;
    private TimeSpan _prevRawOsIdle = TimeSpan.Zero;
    private TimeSpan _wakeOsIdleBaseline = TimeSpan.Zero;
    private DateTime _lastTickUtc = DateTime.UtcNow;
    private DateTime _lastInteractionUtc = DateTime.UtcNow;

    public TimeSpan GetIdleTime()
    {
        if (!OperatingSystem.IsWindows()) return TimeSpan.Zero;

        lock (_gate)
        {
            var nowUtc = DateTime.UtcNow;
            var wokeFromSleep = (nowUtc - _lastTickUtc).TotalSeconds > 5;
            if (wokeFromSleep)
            {
                _lastInteractionUtc = nowUtc;
                _hasLastPoint = false;
            }
            _lastTickUtc = nowUtc;

            // 1. Legge il tempo di inattività dall'API di sistema standard (GetLastInputInfo) con sottrazione unsigned anti-wrap (49.7 giorni)
            var rawOsIdle = TimeSpan.Zero;
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref info))
            {
                var tick = GetTickCount();
                var elapsedMs = unchecked(tick - info.dwTime);
                rawOsIdle = TimeSpan.FromMilliseconds(elapsedMs);
            }

            if (wokeFromSleep)
            {
                // GetLastInputInfo include il tempo di sospensione: imposta una baseline fino al primo input reale.
                _wakeOsIdleBaseline = rawOsIdle;
                _prevRawOsIdle = rawOsIdle;
            }
            else if (rawOsIdle < _wakeOsIdleBaseline)
            {
                _wakeOsIdleBaseline = TimeSpan.Zero;
            }

            var osIdle = rawOsIdle - _wakeOsIdleBaseline;
            if (osIdle < TimeSpan.Zero) osIdle = TimeSpan.Zero;

            // 2. Distingue input reale (tastiera/click/rotellina oppure cursore > 2px) dal micro-jitter del sensore mouse (1-2px)
            var cursorMovedReal = false;
            var cursorJitterOnly = false;
            if (GetCursorPos(out var currentPoint))
            {
                if (!_hasLastPoint)
                {
                    _lastPoint = currentPoint;
                    _hasLastPoint = true;
                    _lastInteractionUtc = nowUtc;
                }
                else
                {
                    var dx = Math.Abs(currentPoint.X - _lastPoint.X);
                    var dy = Math.Abs(currentPoint.Y - _lastPoint.Y);
                    if (dx > 2 || dy > 2)
                    {
                        _lastPoint = currentPoint;
                        cursorMovedReal = true;
                    }
                    else if (dx > 0 || dy > 0)
                    {
                        cursorJitterOnly = true;
                    }
                }
            }

            // Input da tastiera, click o rotellina: rawOsIdle cala rispetto al tick precedente senza micro-jitter del cursore
            var nonCursorInput = !cursorJitterOnly && rawOsIdle < _prevRawOsIdle;
            _prevRawOsIdle = rawOsIdle;

            if (cursorMovedReal || nonCursorInput)
            {
                _lastInteractionUtc = nowUtc;
            }

            var appIdle = nowUtc - _lastInteractionUtc;
            if (appIdle < TimeSpan.Zero) appIdle = TimeSpan.Zero;

            return osIdle > appIdle ? osIdle : appIdle;
        }
    }
}
