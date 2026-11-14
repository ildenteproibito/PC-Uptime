using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Uptime.Core;

namespace Uptime.Presentation.Services;

public static class AppUpdateService
{
    public const string ManifestUrl = "https://uptime-pc.vercel.app/downloads/version.json";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public static string CurrentVersion
    {
        get
        {
            var v = typeof(AppUpdateService).Assembly.GetName().Version;
            return v is null ? "2.0.0" : $"{Math.Max(0, v.Major)}.{Math.Max(0, v.Minor)}.{Math.Max(0, v.Build)}";
        }
    }

    public static async Task<(bool Available, UpdateManifest? Manifest)> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        var url = $"{ManifestUrl}?t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
        if (!UpdateManifestValidator.IsValidManifest(manifest))
        {
            throw new InvalidOperationException("Invalid update manifest.");
        }

        var isNewer = UpdateManifestValidator.IsNewerVersion(CurrentVersion, manifest!.version);
        return (isNewer, manifest);
    }

    public static async Task DownloadAndApplyAsync(
        UpdateManifest manifest,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        if (!UpdateManifestValidator.IsValidManifest(manifest))
            throw new InvalidOperationException("Invalid update manifest.");

        var workRoot = Path.Combine(Path.GetTempPath(), "UptimeUpdate");
        var versionDir = Path.Combine(workRoot, manifest.version.Trim());
        var zipPath = Path.Combine(versionDir, "update.zip");
        var extractDir = Path.Combine(versionDir, "extracted");

        if (Directory.Exists(versionDir))
            Directory.Delete(versionDir, true);
        Directory.CreateDirectory(extractDir);

        using (var response = await Http.GetAsync(manifest.downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;

            await using var netStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await netStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                totalRead += read;
                if (totalBytes > 0)
                    progress?.Report(Math.Clamp((double)totalRead / totalBytes * 100.0, 0, 100));
            }
        }

        progress?.Report(100);

        // Verify SHA-256 before extracting anything
        var actualHash = await ComputeSha256HexAsync(zipPath, ct);
        if (!string.Equals(actualHash, manifest.sha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(zipPath); } catch { }
            throw new InvalidOperationException("SHA-256 checksum mismatch.");
        }

        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);
        var newExe = Path.Combine(extractDir, "Uptime.exe");
        if (!File.Exists(newExe))
            throw new InvalidOperationException("Downloaded archive does not contain Uptime.exe.");

        var targetDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var currentPid = Environment.ProcessId;
        var scriptPath = Path.Combine(workRoot, "apply-update.ps1");

        static string PsQuote(string s) => s.Replace("'", "''");
        var psExtract = PsQuote(extractDir);
        var psTarget = PsQuote(targetDir);
        var psExe = PsQuote(Path.Combine(targetDir, "Uptime.exe"));
        var psWork = PsQuote(workRoot);

        var script = $$"""
$ErrorActionPreference = 'SilentlyContinue'
Wait-Process -Id {{currentPid}} -Timeout 15 -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 450
Copy-Item -Path '{{psExtract}}\*' -Destination '{{psTarget}}' -Recurse -Force
Start-Process -FilePath '{{psExe}}' -WorkingDirectory '{{psTarget}}'
Start-Sleep -Seconds 2
Remove-Item -Path '{{psWork}}' -Recurse -Force -ErrorAction SilentlyContinue
""";

        await File.WriteAllTextAsync(scriptPath, script, System.Text.Encoding.UTF8, ct);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (Application.Current is App app)
        {
            app.RequestExit();
        }
        else if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(0);
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private static async Task<string> ComputeSha256HexAsync(string filePath, CancellationToken ct)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var hash = await SHA256.HashDataAsync(fs, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
