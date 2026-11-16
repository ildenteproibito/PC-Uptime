using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32.SafeHandles;
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

    #region Win32 Battery Device SetupAPI & IOCTL

    // GUID_DEVCLASS_BATTERY: {72631E54-78A4-11D0-BCF7-00AA00B7B32A}
    private static readonly Guid GUID_DEVCLASS_BATTERY = new("72631E54-78A4-11D0-BCF7-00AA00B7B32A");

    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DIGCF_DEVICEINTERFACE = 0x00000010;

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    private const uint IOCTL_BATTERY_QUERY_TAG = 0x00294040;
    private const uint IOCTL_BATTERY_QUERY_INFORMATION = 0x00294044;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public uint cbSize;
        public Guid InterfaceClassGuid;
        public uint Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_QUERY_INFORMATION
    {
        public uint BatteryTag;
        public int InformationLevel;
        public int AtRate;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BATTERY_INFORMATION
    {
        public uint Capabilities;
        public byte Technology;
        public byte Reserved0;
        public byte Reserved1;
        public byte Reserved2;
        public byte Chemistry0;
        public byte Chemistry1;
        public byte Chemistry2;
        public byte Chemistry3;
        public uint DesignedCapacity;
        public uint FullChargedCapacity;
        public uint DefaultAlert1;
        public uint DefaultAlert2;
        public uint CriticalBias;
        public uint CycleCount;
    }

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevs(
        ref Guid ClassGuid,
        IntPtr Enumerator,
        IntPtr hwndParent,
        uint Flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr DeviceInfoSet,
        IntPtr DeviceInfoData,
        ref Guid InterfaceClassGuid,
        uint MemberIndex,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr DeviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
        IntPtr DeviceInterfaceDetailData,
        uint DeviceInterfaceDetailDataSize,
        out uint RequiredSize,
        IntPtr DeviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    #endregion

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
        var isCharging = (status.BatteryFlag & 8) != 0;
        var statusText = isCharging
            ? (percent == 100 ? localizer["BatteryCharged"] : localizer["BatteryCharging"])
            : (status.ACLineStatus == 1 ? localizer["BatteryCharged"] : localizer["BatteryDischarging"]);

        int? designCap = null;
        int? fullCap = null;
        int? cycles = null;

        // Tier 1: Low-level Win32 IOCTL via Device Interfaces (kernel driver level, universal across OEMs)
        try
        {
            QueryFromDeviceIoControl(ref designCap, ref fullCap, ref cycles);
        }
        catch
        {
            // Fall through to next tier
        }

        // Tier 2: WinRT Aggregate Battery API (built-in Windows 10/11 system service)
        if (designCap == null || fullCap == null)
        {
            try
            {
                var report = Windows.Devices.Power.Battery.AggregateBattery.GetReport();
                if (designCap == null && report.DesignCapacityInMilliwattHours is int d && d > 0)
                {
                    designCap = d;
                }
                if (fullCap == null && report.FullChargeCapacityInMilliwattHours is int f && f > 0)
                {
                    fullCap = f;
                }
            }
            catch
            {
                // Fall through
            }
        }

        // Tier 3: Windows powercfg /batteryreport XML fallback (reads firmware/OS cycle count when IOCTL returns 0)
        if (cycles == null || designCap == null || fullCap == null)
        {
            TryReadFromPowerCfgReport(ref designCap, ref fullCap, ref cycles);
        }

        int? health = null;
        if (designCap.HasValue && fullCap.HasValue && designCap.Value > 0)
        {
            health = Math.Clamp((int)Math.Round((double)fullCap.Value / designCap.Value * 100.0), 0, 100);
        }

        return new BatteryInfo(true, percent, isCharging, health, designCap, fullCap, cycles, statusText);
    }

    private static readonly object ReportCacheLock = new();
    private static DateTime _lastReportUtc = DateTime.MinValue;
    private static bool _reportRefreshInFlight;
    private static (int? Design, int? Full, int? Cycles) _cachedReport;

    private static void TryReadFromPowerCfgReport(ref int? designCap, ref int? fullCap, ref int? cycles)
    {
        (int? Design, int? Full, int? Cycles) snap;
        var shouldRefresh = false;
        lock (ReportCacheLock)
        {
            snap = _cachedReport;
            if ((DateTime.UtcNow - _lastReportUtc).TotalMinutes >= 10 && !_reportRefreshInFlight)
            {
                _reportRefreshInFlight = true;
                shouldRefresh = true;
            }
        }

        if (designCap == null) designCap = snap.Design;
        if (fullCap == null) fullCap = snap.Full;
        if (cycles == null) cycles = snap.Cycles;

        if (!shouldRefresh) return;

        _ = Task.Run(() =>
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"uptime-bat-{Guid.NewGuid():N}.xml");
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = $"/batteryreport /xml /output \"{tempPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                if (proc == null) return;
                if (!proc.WaitForExit(5000))
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    return;
                }
                if (!File.Exists(tempPath)) return;

                var doc = XDocument.Load(tempPath);
                int totalDesign = 0;
                int totalFull = 0;
                int? maxCycles = null;

                foreach (var bat in doc.Descendants().Where(e => e.Name.LocalName == "Battery"))
                {
                    var dStr = bat.Elements().FirstOrDefault(e => e.Name.LocalName == "DesignCapacity")?.Value;
                    var fStr = bat.Elements().FirstOrDefault(e => e.Name.LocalName == "FullChargeCapacity")?.Value;
                    var cStr = bat.Elements().FirstOrDefault(e => e.Name.LocalName == "CycleCount")?.Value;

                    if (int.TryParse(dStr, out var d) && d > 0) totalDesign += d;
                    if (int.TryParse(fStr, out var f) && f > 0) totalFull += f;
                    if (int.TryParse(cStr, out var c) && c >= 0)
                    {
                        if (!maxCycles.HasValue || c > maxCycles.Value) maxCycles = c;
                    }
                }

                var updated = (
                    totalDesign > 0 ? (int?)totalDesign : null,
                    totalFull > 0 ? (int?)totalFull : null,
                    maxCycles);

                lock (ReportCacheLock)
                {
                    _cachedReport = updated;
                }
            }
            catch
            {
                // Ignore powercfg fallback failure
            }
            finally
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                lock (ReportCacheLock)
                {
                    _lastReportUtc = DateTime.UtcNow;
                    _reportRefreshInFlight = false;
                }
            }
        });
    }

    private static void QueryFromDeviceIoControl(ref int? designCap, ref int? fullCap, ref int? cycles)
    {
        var guid = GUID_DEVCLASS_BATTERY;
        var hDevInfo = SetupDiGetClassDevs(ref guid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1)) return;

        try
        {
            int totalDesign = 0;
            int totalFull = 0;
            int maxCycles = 0;

            var did = new SP_DEVICE_INTERFACE_DATA();
            did.cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>();

            uint index = 0;
            while (SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref guid, index++, ref did))
            {
                SetupDiGetDeviceInterfaceDetail(hDevInfo, ref did, IntPtr.Zero, 0, out uint requiredSize, IntPtr.Zero);
                if (requiredSize == 0) continue;

                var pDetail = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    // Size of the fixed header portion (8 on x64, 6 on x86 Unicode)
                    Marshal.WriteInt32(pDetail, IntPtr.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(hDevInfo, ref did, pDetail, requiredSize, out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    // DevicePath is at offset 4
                    var devicePath = Marshal.PtrToStringAuto(pDetail + 4);
                    if (string.IsNullOrEmpty(devicePath)) continue;

                    using var handle = OpenBatteryHandle(devicePath);
                    if (handle.IsInvalid) continue;

                    if (QueryBatteryInformation(handle, out var bInfo))
                    {
                        if (bInfo.DesignedCapacity > 0 && bInfo.DesignedCapacity != 0xFFFFFFFF)
                        {
                            totalDesign += (int)bInfo.DesignedCapacity;
                        }
                        if (bInfo.FullChargedCapacity > 0 && bInfo.FullChargedCapacity != 0xFFFFFFFF)
                        {
                            totalFull += (int)bInfo.FullChargedCapacity;
                        }
                        if (bInfo.CycleCount > 0 && bInfo.CycleCount != 0xFFFFFFFF)
                        {
                            if ((int)bInfo.CycleCount > maxCycles)
                            {
                                maxCycles = (int)bInfo.CycleCount;
                            }
                        }
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pDetail);
                }
            }

            if (totalDesign > 0) designCap = totalDesign;
            if (totalFull > 0) fullCap = totalFull;
            if (maxCycles > 0) cycles = maxCycles;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }
    }

    private static SafeFileHandle OpenBatteryHandle(string devicePath)
    {
        var handle = CreateFile(
            devicePath,
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            handle = CreateFile(
                devicePath,
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                IntPtr.Zero,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                IntPtr.Zero);
        }

        return handle;
    }

    private static bool QueryBatteryInformation(SafeFileHandle handle, out BATTERY_INFORMATION info)
    {
        info = default;

        // 1. Query battery tag
        var pWait = Marshal.AllocHGlobal(sizeof(uint));
        var pTag = Marshal.AllocHGlobal(sizeof(uint));
        uint tag = 0;
        try
        {
            Marshal.WriteInt32(pWait, 0); // 0 ms timeout
            if (!DeviceIoControl(handle, IOCTL_BATTERY_QUERY_TAG, pWait, sizeof(uint), pTag, sizeof(uint), out _, IntPtr.Zero))
            {
                return false;
            }
            tag = (uint)Marshal.ReadInt32(pTag);
        }
        finally
        {
            Marshal.FreeHGlobal(pWait);
            Marshal.FreeHGlobal(pTag);
        }

        if (tag == 0 || tag == 0xFFFFFFFF) return false;

        // 2. Query BATTERY_INFORMATION
        var bqi = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = tag,
            InformationLevel = 0, // BatteryInformation
            AtRate = 0
        };

        int inSize = Marshal.SizeOf<BATTERY_QUERY_INFORMATION>();
        int outSize = Marshal.SizeOf<BATTERY_INFORMATION>();

        var pIn = Marshal.AllocHGlobal(inSize);
        var pOut = Marshal.AllocHGlobal(outSize);
        try
        {
            Marshal.StructureToPtr(bqi, pIn, false);
            if (DeviceIoControl(handle, IOCTL_BATTERY_QUERY_INFORMATION, pIn, (uint)inSize, pOut, (uint)outSize, out _, IntPtr.Zero))
            {
                info = Marshal.PtrToStructure<BATTERY_INFORMATION>(pOut);
                return true;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(pIn);
            Marshal.FreeHGlobal(pOut);
        }

        return false;
    }
}
