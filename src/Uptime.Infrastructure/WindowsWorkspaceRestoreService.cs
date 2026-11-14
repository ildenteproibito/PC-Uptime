using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class WindowsWorkspaceRestoreService : IWorkspaceRestoreService
{
    private static readonly string SnapshotPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "workspace_restore.json");

    private static readonly HashSet<string> ExcludedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Uptime",
        "explorer",
        "ApplicationFrameHost",
        "TextInputHost",
        "SystemSettings",
        "ShellExperienceHost",
        "SearchHost",
        "SearchApp",
        "StartMenuExperienceHost",
        "LockApp",
        "NVIDIA Overlay",
        "Taskmgr"
    };

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record WorkspaceSnapshot(List<string> AppPaths, List<string> FolderPaths);

    public void SaveWorkspaceSnapshot()
    {
        if (!OperatingSystem.IsWindows()) return;

        var appPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processes = Process.GetProcesses();
        try
        {
            foreach (var p in processes)
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    if (ExcludedProcessNames.Contains(p.ProcessName)) continue;

                    var fileName = p.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(fileName) && File.Exists(fileName))
                    {
                        // Skip Windows system apps inside System32/WinApps
                        if (fileName.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.Windows), StringComparison.OrdinalIgnoreCase))
                            continue;
                        appPaths.Add(fileName);
                    }
                }
                catch
                {
                    // Access denied or exited process — skip safely
                }
            }
        }
        finally
        {
            foreach (var p in processes)
            {
                try { p.Dispose(); } catch { }
            }
        }

        var folderPaths = GetOpenExplorerFolders();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath)!);
            var json = JsonSerializer.Serialize(new WorkspaceSnapshot(appPaths.ToList(), folderPaths));
            var tmpPath = SnapshotPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, SnapshotPath, overwrite: true);

            var selfExe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(selfExe))
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\RunOnce");
                key?.SetValue("UptimeWorkspaceRestore", $"\"{selfExe}\" --restore-workspace");
            }
        }
        catch
        {
            // Silent fail if registry/disk unavailable
        }
    }

    public void RestoreWorkspaceIfPending()
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(SnapshotPath)) return;

        WorkspaceSnapshot? snapshot = null;
        try
        {
            var raw = File.ReadAllText(SnapshotPath);
            File.Delete(SnapshotPath);
            snapshot = JsonSerializer.Deserialize<WorkspaceSnapshot>(raw);
        }
        catch
        {
            try { if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath); } catch { }
            return;
        }

        if (snapshot is null) return;

        foreach (var app in snapshot.AppPaths)
        {
            try
            {
                if (File.Exists(app))
                {
                    using var _ = Process.Start(new ProcessStartInfo(app) { UseShellExecute = true });
                }
            }
            catch { }
        }

        foreach (var folder in snapshot.FolderPaths)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    using var _ = Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
                }
            }
            catch { }
        }
    }

    private static List<string> GetOpenExplorerFolders()
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        dynamic? shell = null;
        dynamic? windows = null;
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return new List<string>();
            shell = Activator.CreateInstance(shellType);
            if (shell == null) return new List<string>();

            windows = shell.Windows();
            foreach (var win in windows)
            {
                try
                {
                    string? url = win.LocationURL as string;
                    if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile)
                    {
                        var localPath = uri.LocalPath;
                        if (Directory.Exists(localPath))
                            folders.Add(localPath);
                    }
                }
                catch { }
                finally
                {
                    if (win != null && System.Runtime.InteropServices.Marshal.IsComObject(win))
                        System.Runtime.InteropServices.Marshal.FinalReleaseComObject(win);
                }
            }
        }
        catch
        {
            // COM unavailable
        }
        finally
        {
            if (windows != null && System.Runtime.InteropServices.Marshal.IsComObject(windows))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(windows);
            if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell))
                System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
        return folders.ToList();
    }
}
