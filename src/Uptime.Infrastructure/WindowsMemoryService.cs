using System.Diagnostics;
using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsMemoryService : IMemoryService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    public MemoryStatusInfo GetStatus()
    {
        var stat = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref stat) || stat.ullTotalPhys == 0)
            return new MemoryStatusInfo(0, 0, 0, 0);

        var totalGb = stat.ullTotalPhys / (1024d * 1024d * 1024d);
        var availMb = (long)(stat.ullAvailPhys / (1024UL * 1024UL));
        var usedGb = (stat.ullTotalPhys - stat.ullAvailPhys) / (1024d * 1024d * 1024d);
        return new MemoryStatusInfo((int)stat.dwMemoryLoad, usedGb, totalGb, availMb);
    }

    public MemoryTrimResult TrimBackgroundWorkingSets()
    {
        var before = GetStatus();
        var currentPid = Environment.ProcessId;
        var trimmed = 0;

        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (proc.Id <= 4 || proc.Id == currentPid) continue;
                if (EmptyWorkingSet(proc.Handle))
                    trimmed++;
            }
            catch
            {
                // System/protected processes or exited processes cannot be trimmed
            }
            finally
            {
                proc.Dispose();
            }
        }

        var after = GetStatus();
        var freedMb = Math.Max(0, after.AvailableMb - before.AvailableMb);
        return new MemoryTrimResult(freedMb, trimmed, after);
    }
}
