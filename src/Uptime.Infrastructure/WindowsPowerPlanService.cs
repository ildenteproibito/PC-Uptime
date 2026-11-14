using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsPowerPlanService : IPowerPlanService
{
    private static readonly Guid BalancedGuid = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid HighPerfGuid = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid PowerSaverGuid = new("a1841308-3541-4fab-bc81-f71556f20b4a");

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    [DllImport("powrprof.dll", CharSet = CharSet.Unicode)]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey,
        ref Guid SchemeGuid,
        IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid,
        IntPtr Buffer,
        ref uint BufferSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public string GetActivePlanName()
    {
        if (!OperatingSystem.IsWindows()) return PowerPlanKind.Unknown.ToString();

        var ptr = IntPtr.Zero;
        var nameBuf = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out ptr) == 0 && ptr != IntPtr.Zero)
            {
                var guid = Marshal.PtrToStructure<Guid>(ptr);
                uint size = 0;
                PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
                if (size > 0 && size <= 4096)
                {
                    nameBuf = Marshal.AllocHGlobal((int)size);
                    if (PowerReadFriendlyName(IntPtr.Zero, ref guid, IntPtr.Zero, IntPtr.Zero, nameBuf, ref size) == 0)
                    {
                        var friendly = Marshal.PtrToStringUni(nameBuf, (int)(size / 2))?.TrimEnd('\0').Trim();
                        if (!string.IsNullOrWhiteSpace(friendly))
                            return friendly;
                    }
                }
            }
        }
        catch
        {
            // Fall back to enum name below
        }
        finally
        {
            if (nameBuf != IntPtr.Zero) Marshal.FreeHGlobal(nameBuf);
            if (ptr != IntPtr.Zero) LocalFree(ptr);
        }

        return GetActivePlan().ToString();
    }

    public PowerPlanKind GetActivePlan()
    {
        if (!OperatingSystem.IsWindows()) return PowerPlanKind.Unknown;

        var ptr = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out ptr) == 0 && ptr != IntPtr.Zero)
            {
                var guid = Marshal.PtrToStructure<Guid>(ptr);
                if (guid == BalancedGuid) return PowerPlanKind.Balanced;
                if (guid == HighPerfGuid) return PowerPlanKind.HighPerformance;
                if (guid == PowerSaverGuid) return PowerPlanKind.PowerSaver;
            }
        }
        catch
        {
            // Ignore unsupported environments
        }
        finally
        {
            if (ptr != IntPtr.Zero) LocalFree(ptr);
        }

        return PowerPlanKind.Unknown;
    }

    public bool SetActivePlan(PowerPlanKind plan)
    {
        if (!OperatingSystem.IsWindows()) return false;

        var guid = plan switch
        {
            PowerPlanKind.Balanced => BalancedGuid,
            PowerPlanKind.HighPerformance => HighPerfGuid,
            PowerPlanKind.PowerSaver => PowerSaverGuid,
            _ => Guid.Empty,
        };

        if (guid == Guid.Empty) return false;

        try
        {
            return PowerSetActiveScheme(IntPtr.Zero, ref guid) == 0;
        }
        catch
        {
            return false;
        }
    }
}
