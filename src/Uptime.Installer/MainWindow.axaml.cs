using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Win32;

namespace Uptime.Installer;

public partial class MainWindow : Window
{
    private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Uptime";
    private const string RunRegPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppExeName = "Uptime.exe";
    private const string UninstallExeName = "Uninstall.exe";

    private enum Stage { Options, Installing, Done, UninstallConfirm, Uninstalled }

    private string _lang = InstallerStrings.DefaultLang();
    private Stage _stage;
    private string _installedDir = "";

    public MainWindow()
    {
        InitializeComponent();

        var defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Uptime");

        if (Program.IsUninstallMode)
        {
            _installedDir = !string.IsNullOrWhiteSpace(Program.UninstallTargetDir)
                ? Program.UninstallTargetDir
                : ResolveInstalledDir(defaultDir);
            _stage = Stage.UninstallConfirm;
            UninstallPathText.Text = _installedDir;
        }
        else
        {
            _stage = Stage.Options;
            InstallPathBox.Text = ResolveInstalledDir(defaultDir);
        }

        ApplyLanguage();
        UpdateStageUi();
    }

    private static string ResolveInstalledDir(string fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(UninstallRegPath);
            if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc))
            {
                return loc;
            }
        }
        catch { }
        return fallback;
    }

    private string T(string key) => InstallerStrings.Get(_lang, key);

    private void ApplyLanguage()
    {
        HeaderSubtitleText.Text = T("HeaderSubtitle");
        HeroTitleText.Text = T("HeroTitle");
        HeroDescText.Text = T("HeroDesc");
        PathLabelText.Text = T("PathLabel");
        BrowseBtn.Content = T("Browse");
        OptDesktopText.Text = T("OptDesktop");
        OptStartMenuText.Text = T("OptStartMenu");
        OptStartupText.Text = T("OptStartup");
        InstallingTitleText.Text = _stage == Stage.UninstallConfirm || Program.IsUninstallMode
            ? T("UninstallingTitle")
            : T("InstallingTitle");
        InstallingHintText.Text = T("InstallingHint");
        DoneTitleText.Text = Program.IsUninstallMode ? T("UninstallDoneTitle") : T("DoneTitle");
        DoneDescText.Text = Program.IsUninstallMode ? T("UninstallDoneDesc") : T("DoneDesc");
        LaunchAfterText.Text = T("LaunchAfter");
        UninstallTitleText.Text = T("UninstallTitle");
        UninstallDescText.Text = T("UninstallDesc");
        CancelBtn.Content = T("CancelBtn");

        LangItBtn.BorderBrush = Brush.Parse(_lang == "it" ? "#7BC4A8" : "#2A2E36");
        LangEnBtn.BorderBrush = Brush.Parse(_lang == "en" ? "#7BC4A8" : "#2A2E36");
        LangRuBtn.BorderBrush = Brush.Parse(_lang == "ru" ? "#7BC4A8" : "#2A2E36");

        UpdatePrimaryButtonLabel();
    }

    private void UpdatePrimaryButtonLabel()
    {
        PrimaryBtn.Content = _stage switch
        {
            Stage.Options => T("InstallBtn"),
            Stage.UninstallConfirm => T("UninstallBtn"),
            Stage.Done or Stage.Uninstalled => T("FinishBtn"),
            _ => T("InstallBtn")
        };
    }

    private void UpdateStageUi()
    {
        OptionsPanel.IsVisible = _stage == Stage.Options;
        ProgressPanel.IsVisible = _stage == Stage.Installing;
        DonePanel.IsVisible = _stage is Stage.Done or Stage.Uninstalled;
        UninstallPanel.IsVisible = _stage == Stage.UninstallConfirm;
        LaunchCard.IsVisible = _stage == Stage.Done;
        DonePathText.IsVisible = _stage == Stage.Done;

        CancelBtn.IsVisible = _stage is Stage.Options or Stage.UninstallConfirm;
        PrimaryBtn.IsVisible = _stage != Stage.Installing;
        CloseBtn.IsEnabled = _stage != Stage.Installing;

        PrimaryBtn.Classes.Set("accent", _stage != Stage.UninstallConfirm);
        PrimaryBtn.Classes.Set("danger", _stage == Stage.UninstallConfirm);
        UpdatePrimaryButtonLabel();
    }

    private void OnLangClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string code } && code is "it" or "en" or "ru")
        {
            _lang = code;
            ApplyLanguage();
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = T("PickFolder"),
            AllowMultiple = false
        });
        if (folders is { Count: > 0 } && folders[0].TryGetLocalPath() is { Length: > 0 } chosen)
        {
            InstallPathBox.Text = chosen.EndsWith("Uptime", StringComparison.OrdinalIgnoreCase)
                ? chosen
                : Path.Combine(chosen, "Uptime");
        }
    }

    private async void OnPrimaryClick(object? sender, RoutedEventArgs e)
    {
        if (_stage == Stage.Options)
        {
            await RunInstallAsync();
        }
        else if (_stage == Stage.UninstallConfirm)
        {
            await RunUninstallAsync();
        }
        else if (_stage == Stage.Done)
        {
            if (LaunchAfterToggle.IsChecked == true)
            {
                var exe = Path.Combine(_installedDir, AppExeName);
                if (File.Exists(exe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
                    }
                    catch { }
                }
            }
            Close();
        }
        else if (_stage == Stage.Uninstalled)
        {
            Close();
        }
    }

    private void SetProgress(double value, string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            InstallProgressBar.Value = value;
            ProgressPercentText.Text = $"{(int)Math.Round(value)}%";
            ProgressStepText.Text = text;
        });
    }

    private async Task RunInstallAsync()
    {
        ErrorText.IsVisible = false;
        var targetDir = (InstallPathBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(targetDir)) return;

        var createDesktop = DesktopToggle.IsChecked == true;
        var createStartMenu = StartMenuToggle.IsChecked == true;
        var runAtStartup = StartupToggle.IsChecked == true;

        _stage = Stage.Installing;
        ApplyLanguage();
        UpdateStageUi();

        try
        {
            SetProgress(8, T("StepClosing"));
            await Task.Run(StopRunningUptime);

            SetProgress(18, T("StepExtracting"));
            await Task.Run(() => ExtractPayload(targetDir));

            SetProgress(90, T("StepShortcuts"));
            await Task.Run(() => ConfigureSystemIntegration(targetDir, createDesktop, createStartMenu, runAtStartup));

            _installedDir = targetDir;
            DonePathText.Text = targetDir;
            SetProgress(100, T("StepDone"));
            await Task.Delay(250);

            _stage = Stage.Done;
            ApplyLanguage();
            UpdateStageUi();
        }
        catch (Exception ex)
        {
            _stage = Stage.Options;
            ApplyLanguage();
            UpdateStageUi();
            ErrorText.Text = string.Format(T("ErrFailed"), ex.Message);
            ErrorText.IsVisible = true;
        }
    }

    private static void StopRunningUptime()
    {
        foreach (var proc in Process.GetProcessesByName("Uptime"))
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(3000);
                }
            }
            catch { }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private void ExtractPayload(string targetDir)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var gzStream = asm.GetManifestResourceStream("Uptime.Installer.Payload.Uptime.exe.gz")
            ?? throw new InvalidOperationException(T("ErrNoPayload"));

        Directory.CreateDirectory(targetDir);
        var exePath = Path.Combine(targetDir, AppExeName);
        var tmpPath = exePath + ".tmp";

        using (var gzip = new GZipStream(gzStream, CompressionMode.Decompress))
        using (var outFs = File.Create(tmpPath))
        {
            var buffer = new byte[128 * 1024];
            long totalRead = 0;
            // Approximate uncompressed size (~168 MB) for smooth progress feedback
            const double approxSize = 168_000_000.0;
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                outFs.Write(buffer, 0, read);
                totalRead += read;
                var pct = 18.0 + Math.Min(68.0, (totalRead / approxSize) * 68.0);
                SetProgress(pct, T("StepExtracting"));
            }
        }

        File.Move(tmpPath, exePath, overwrite: true);

        using var svgStream = asm.GetManifestResourceStream("Uptime.Installer.Payload.favicon.svg");
        if (svgStream is not null)
        {
            var assetsDir = Path.Combine(targetDir, "Assets");
            Directory.CreateDirectory(assetsDir);
            using var svgOut = File.Create(Path.Combine(assetsDir, "favicon.svg"));
            svgStream.CopyTo(svgOut);
        }

        // Copy installer executable as Uninstall.exe so Windows Installed Apps can uninstall offline
        if (Environment.ProcessPath is { Length: > 0 } selfPath && File.Exists(selfPath))
        {
            var uninstallPath = Path.Combine(targetDir, UninstallExeName);
            if (!string.Equals(Path.GetFullPath(selfPath), Path.GetFullPath(uninstallPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(selfPath, uninstallPath, overwrite: true);
            }
        }
    }

    private static void ConfigureSystemIntegration(string targetDir, bool desktop, bool startMenu, bool startup)
    {
        var exePath = Path.Combine(targetDir, AppExeName);
        var uninstallPath = Path.Combine(targetDir, UninstallExeName);

        if (desktop)
        {
            var desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Uptime.lnk");
            ShortcutHelper.CreateShortcut(desk, exePath, targetDir, "Uptime · System Monitor");
        }

        if (startMenu)
        {
            var smDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Uptime");
            Directory.CreateDirectory(smDir);
            ShortcutHelper.CreateShortcut(Path.Combine(smDir, "Uptime.lnk"), exePath, targetDir, "Uptime · System Monitor");
        }

        using (var runKey = Registry.CurrentUser.CreateSubKey(RunRegPath))
        {
            if (startup) runKey.SetValue("Uptime", $"\"{exePath}\"");
            else runKey.DeleteValue("Uptime", throwOnMissingValue: false);
        }

        var cleanTargetDir = Path.TrimEndingDirectorySeparator(targetDir);
        using var uninstKey = Registry.CurrentUser.CreateSubKey(UninstallRegPath);
        uninstKey.SetValue("DisplayName", "Uptime");
        uninstKey.SetValue("DisplayVersion", "2.0.0");
        uninstKey.SetValue("Publisher", "IL DENTE PROIBITO");
        uninstKey.SetValue("DisplayIcon", exePath);
        uninstKey.SetValue("InstallLocation", cleanTargetDir);
        uninstKey.SetValue("UninstallString", $"\"{uninstallPath}\" --uninstall \"--dir={cleanTargetDir}\"");
        uninstKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
        uninstKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        uninstKey.SetValue("EstimatedSize", 168000, RegistryValueKind.DWord);
    }

    private async Task RunUninstallAsync()
    {
        _stage = Stage.Installing;
        ApplyLanguage();
        UpdateStageUi();

        try
        {
            SetProgress(20, T("StepClosing"));
            await Task.Run(StopRunningUptime);

            SetProgress(65, T("UninstallingTitle"));
            await Task.Run(() =>
            {
                var desk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Uptime.lnk");
                try { if (File.Exists(desk)) File.Delete(desk); } catch { }

                var smDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "Uptime");
                try { if (Directory.Exists(smDir)) Directory.Delete(smDir, true); } catch { }

                try
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(RunRegPath, writable: true);
                    runKey?.DeleteValue("Uptime", throwOnMissingValue: false);
                }
                catch { }

                try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, throwOnMissingSubKey: false); } catch { }

                if (!string.IsNullOrWhiteSpace(_installedDir) && Directory.Exists(_installedDir))
                {
                    var exePath = Path.Combine(_installedDir, AppExeName);
                    try { if (File.Exists(exePath)) File.Delete(exePath); } catch { }

                    var assetsDir = Path.Combine(_installedDir, "Assets");
                    try { if (Directory.Exists(assetsDir)) Directory.Delete(assetsDir, true); } catch { }

                    var selfPath = Environment.ProcessPath ?? "";
                    var normalizedTarget = Path.TrimEndingDirectorySeparator(_installedDir) + Path.DirectorySeparatorChar;
                    var runningInsideTarget = selfPath.StartsWith(normalizedTarget, StringComparison.OrdinalIgnoreCase);
                    if (runningInsideTarget)
                    {
                        var pid = Environment.ProcessId;
                        var escapedDir = Path.TrimEndingDirectorySeparator(_installedDir).Replace("'", "''");
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"Wait-Process -Id {pid} -ErrorAction SilentlyContinue; Start-Sleep -Milliseconds 300; Remove-Item -LiteralPath '{escapedDir}' -Recurse -Force -ErrorAction SilentlyContinue\"",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        });
                    }
                    else
                    {
                        try { Directory.Delete(_installedDir, true); } catch { }
                    }
                }
            });

            SetProgress(100, T("StepDone"));
            await Task.Delay(250);
            _stage = Stage.Uninstalled;
            ApplyLanguage();
            UpdateStageUi();
        }
        catch (Exception ex)
        {
            _stage = Stage.UninstallConfirm;
            ApplyLanguage();
            UpdateStageUi();
            ErrorText.Text = ex.Message;
            ErrorText.IsVisible = true;
        }
    }
}
