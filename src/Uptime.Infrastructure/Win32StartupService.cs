using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

/// <summary>Launch on Windows startup via HKCU\...\Run.</summary>
public sealed class Win32StartupService : IStartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ValueName = "Uptime";

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                if (key?.GetValue(ValueName) is not string rawCmd || string.IsNullOrWhiteSpace(rawCmd))
                    return false;

                var currentPath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    var storedPath = rawCmd.Trim().Trim('"');
                    if (!string.Equals(storedPath, currentPath, StringComparison.OrdinalIgnoreCase))
                        return false;
                }

                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
                if (approved?.GetValue(ValueName) is byte[] bytes && bytes.Length > 0 && (bytes[0] & 1) != 0)
                    return false;

                return true;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                return false;
            }
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                var path = Environment.ProcessPath ?? throw new InvalidOperationException("Process executable path is unknown.");
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                key.SetValue(ValueName, $"\"{path}\"");

                using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
                if (approved?.GetValue(ValueName) is not null)
                    approved.SetValue(ValueName, new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, RegistryValueKind.Binary);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Ignore permission-restricted registry failures safely
        }
    }
}
