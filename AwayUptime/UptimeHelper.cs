using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AwayUptime;

public static class UptimeHelper
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	public class MEMORYSTATUSEX
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

		public MEMORYSTATUSEX()
		{
			dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
		}
	}

	private const string StartupRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
	private const string StartupApprovedRegistryKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\StartupApproved\\Run";
	private const string AppName = "Uptime";

	public static TimeSpan GetUptime()
	{
		return TimeSpan.FromMilliseconds(Environment.TickCount64);
	}

	public static DateTime GetBootTime()
	{
		return DateTime.Now - GetUptime();
	}

	public static DateTime GetBootTimeUtc()
	{
		return DateTime.UtcNow - GetUptime();
	}

	public static (string primary, string secondary) FormatDisplay(TimeSpan uptime)
	{
		return LocalizationEngine.Current.FormatDisplay(uptime);
	}

	public static string FormatTrayTooltip(TimeSpan uptime)
	{
		return LocalizationEngine.Current.FormatTooltip(uptime);
	}

	public static string FormatClipboardCopy(TimeSpan uptime, DateTime bootTime)
	{
		return LocalizationEngine.Current.FormatFull(uptime, bootTime);
	}

	public static string GetStartupShortcutPath()
	{
		string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
		return Path.Combine(startupFolder, "Uptime.lnk");
	}

	public static bool IsRunAtStartup()
	{
		try
		{
			string lnkPath = GetStartupShortcutPath();
			if (File.Exists(lnkPath))
			{
				return true;
			}

			using RegistryKey? registryKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: false);
			if (registryKey?.GetValue(AppName) != null)
			{
				using RegistryKey? approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRegistryKey, writable: false);
				if (approvedKey?.GetValue(AppName) is byte[] bytes && bytes.Length > 0 && bytes[0] == 0x03)
				{
					return false;
				}
				return true;
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	public static void SetRunAtStartup(bool enable)
	{
		try
		{
			string exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Uptime.exe");
			string lnkPath = GetStartupShortcutPath();

			using (RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(StartupRegistryKey, writable: true))
			{
				if (runKey != null)
				{
					if (enable)
					{
						runKey.SetValue(AppName, "\"" + exePath + "\"");
					}
					else
					{
						runKey.DeleteValue(AppName, throwOnMissingValue: false);
					}
				}
			}

			using (RegistryKey? approvedKey = Registry.CurrentUser.OpenSubKey(StartupApprovedRegistryKey, writable: true))
			{
				if (approvedKey != null)
				{
					if (enable)
					{
						byte[] enabledBytes = new byte[12] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
						approvedKey.SetValue(AppName, enabledBytes, RegistryValueKind.Binary);
					}
					else
					{
						approvedKey.DeleteValue(AppName, throwOnMissingValue: false);
					}
				}
			}

			if (enable)
			{
				CreateShortcut(lnkPath, exePath, AppContext.BaseDirectory, "Uptime - System Uptime & Stability Monitor");
			}
			else
			{
				if (File.Exists(lnkPath))
				{
					File.Delete(lnkPath);
				}
			}
		}
		catch
		{
		}
	}

	private static void CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string description)
	{
		try
		{
			IShellLinkW link = (IShellLinkW)new ShellLink();
			link.SetPath(targetPath);
			link.SetWorkingDirectory(workingDirectory);
			link.SetDescription(description);
			((IPersistFile)link).Save(shortcutPath, true);
		}
		catch
		{
			try
			{
				Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
				if (shellType != null)
				{
					dynamic shell = Activator.CreateInstance(shellType)!;
					dynamic shortcut = shell.CreateShortcut(shortcutPath);
					shortcut.TargetPath = targetPath;
					shortcut.WorkingDirectory = workingDirectory;
					shortcut.Description = description;
					shortcut.Save();
				}
			}
			catch
			{
			}
		}
	}

	[ComImport]
	[Guid("00021401-0000-0000-C000-000000000046")]
	private class ShellLink { }

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("000214F9-0000-0000-C000-000000000046")]
	private interface IShellLinkW
	{
		void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, nint pfd, uint fFlags);
		void GetIDList(out nint ppidl);
		void SetIDList(nint pidl);
		void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
		void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
		void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
		void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
		void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
		void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
		void GetHotkey(out ushort pwHotkey);
		void SetHotkey(ushort wHotkey);
		void GetShowCmd(out int piShowCmd);
		void SetShowCmd(int iShowCmd);
		void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
		void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
		void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
		void Resolve(nint hwnd, uint fFlags);
		void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
	}

	[ComImport]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	[Guid("0000010b-0000-0000-C000-000000000046")]
	private interface IPersistFile
	{
		void GetClassID(out Guid pClassID);
		void IsDirty();
		void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
		void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
		void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
		void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
	}

	[DllImport("psapi.dll")]
	private static extern bool EmptyWorkingSet(nint hProcess);

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool GlobalMemoryStatusEx([In][Out] MEMORYSTATUSEX lpBuffer);

	public static MEMORYSTATUSEX? GetMemoryStatus()
	{
		try
		{
			var status = new MEMORYSTATUSEX();
			if (GlobalMemoryStatusEx(status))
			{
				return status;
			}
		}
		catch
		{
		}
		return null;
	}

	public static ulong GetAvailablePhysicalMemoryBytes()
	{
		var status = GetMemoryStatus();
		return status?.ullAvailPhys ?? 0uL;
	}

	public static ulong GetTotalPhysicalMemoryBytes()
	{
		var status = GetMemoryStatus();
		return status?.ullTotalPhys ?? 0uL;
	}

	public static (string totalRam, string availRam, uint loadPercent) GetMemoryDisplayInfo()
	{
		var status = GetMemoryStatus();
		if (status != null && status.ullTotalPhys > 0)
		{
			double totalGb = (double)status.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
			double availGb = (double)status.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
			return ($"{totalGb:F1} GB", $"{availGb:F1} GB", status.dwMemoryLoad);
		}
		return ("N/A", "N/A", 0);
	}

	public static string GetProcessorName()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
			string? name = key?.GetValue("ProcessorNameString") as string;
			if (!string.IsNullOrWhiteSpace(name))
			{
				return name.Trim();
			}
		}
		catch
		{
		}
		return $"{Environment.ProcessorCount} Logical Processors";
	}

	public static string GetOsVersionString()
	{
		try
		{
			using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
			if (key != null)
			{
				string? prod = key.GetValue("ProductName") as string;
				string? build = key.GetValue("CurrentBuild") as string;
				string? ubr = key.GetValue("UBR")?.ToString();
				if (!string.IsNullOrEmpty(prod) && !string.IsNullOrEmpty(build))
				{
					return string.IsNullOrEmpty(ubr) ? $"{prod} (Build {build})" : $"{prod} (Build {build}.{ubr})";
				}
			}
		}
		catch
		{
		}
		return RuntimeInformation.OSDescription;
	}

	public static void TrimWorkingSet()
	{
		try
		{
			GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
			GC.WaitForPendingFinalizers();
			EmptyWorkingSet(Process.GetCurrentProcess().Handle);
		}
		catch
		{
		}
	}
}
