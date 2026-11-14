using System.Diagnostics;
using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsKeepAwakeService : IKeepAwakeService
{
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    public bool IsActive { get; private set; }

    public void SetAwake(bool enabled, bool keepDisplayOn = true)
    {
        if (!OperatingSystem.IsWindows())
        {
            IsActive = false;
            return;
        }

        try
        {
            if (enabled)
            {
                var flags = ES_CONTINUOUS | ES_SYSTEM_REQUIRED;
                if (keepDisplayOn) flags |= ES_DISPLAY_REQUIRED;
                var result = SetThreadExecutionState(flags);
                IsActive = result != 0;
            }
            else
            {
                SetThreadExecutionState(ES_CONTINUOUS);
                IsActive = false;
            }
        }
        catch
        {
            IsActive = false;
        }
    }

    public bool IsProcessRunning(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        var clean = processName.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            clean = clean[..^4];
        if (string.IsNullOrWhiteSpace(clean)) return false;

        Process[]? procs = null;
        try
        {
            procs = Process.GetProcessesByName(clean);
            return procs.Length > 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (procs != null)
            {
                foreach (var p in procs)
                {
                    try { p.Dispose(); } catch { }
                }
            }
        }
    }
}
