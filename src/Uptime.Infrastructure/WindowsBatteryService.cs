using System.Management;
using System.Runtime.InteropServices;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsBatteryService : IBatteryService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

    public BatteryInfo GetBatteryStatus()
    {
        var localizer = Localizer.Instance;
        if (!GetSystemPowerStatus(out var status))
        {
            return new BatteryInfo(false, 100, false, null, null, null, null, localizer["BatteryDesktop"]);
        }

        // 128 = No system battery, 255 = Status unknown
        var hasBattery = (status.BatteryFlag & 128) == 0 && status.BatteryLifePercent <= 100;
        if (!hasBattery)
        {
            return new BatteryInfo(false, 100, false, null, null, null, null, localizer["BatteryDesktop"]);
        }

        var percent = Math.Clamp((int)status.BatteryLifePercent, 0, 100);
        var isCharging = (status.BatteryFlag & 8) != 0 || status.ACLineStatus == 1;
        var statusText = isCharging ? (percent == 100 ? localizer["BatteryCharged"] : localizer["BatteryCharging"]) : localizer["BatteryDischarging"];

        int? designCap = null;
        int? fullCap = null;
        int? cycles = null;

        try
        {
            using var searcherStatic = new ManagementObjectSearcher(@"root\wmi", "SELECT DesignedCapacity, CycleCount FROM BatteryStaticData");
            foreach (ManagementObject obj in searcherStatic.Get())
            {
                if (obj["DesignedCapacity"] is uint d && d > 0) designCap = (int)d;
                if (obj["CycleCount"] is uint c) cycles = (int)c;
                break;
            }

            using var searcherFull = new ManagementObjectSearcher(@"root\wmi", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");
            foreach (ManagementObject obj in searcherFull.Get())
            {
                if (obj["FullChargedCapacity"] is uint f && f > 0) fullCap = (int)f;
                break;
            }
        }
        catch
        {
            // Silent fallback if WMI Battery query is unsupported by the hardware
        }

        int? health = null;
        if (designCap.HasValue && fullCap.HasValue && designCap.Value > 0)
        {
            health = Math.Clamp((int)Math.Round((double)fullCap.Value / designCap.Value * 100.0), 0, 100);
        }

        return new BatteryInfo(true, percent, isCharging, health, designCap, fullCap, cycles, statusText);
    }
}
