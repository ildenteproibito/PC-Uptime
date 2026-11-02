using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Uptime.Core;
using Uptime.Infrastructure;

namespace Uptime.Presentation;

internal static class DiagnosticArchive
{
    public static async Task WriteAsync(Stream destination)
    {
        await using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8))
        {
            var about = new
            {
                Application = "Uptime",
                Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                CreatedLocal = DateTimeOffset.Now,
                OperatingSystem = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Language = Localizer.Instance.Language,
                EventLogPeriodDays = 7,
                Privacy = "Generated locally. No data is uploaded by Uptime."
            };
            await AddTextAsync(archive, "about.json", JsonSerializer.Serialize(about, new JsonSerializerOptions { WriteIndented = true }));
            await AddTextAsync(archive, "README.txt",
                "Uptime support diagnostics\r\n\r\n" +
                "uptime.log contains app diagnostics. windows-events.txt contains recent Warning, Error and Critical records from the System and Application Event Viewer channels, including rendered event messages where Windows provides them.\r\n" +
                "The archive is created locally and is not transmitted automatically. Review it before sharing; event messages and app errors may contain device-specific paths or details.\r\n");
            if (File.Exists(AppDiagnostics.LogPath))
                await AddFileAsync(archive, "uptime.log", AppDiagnostics.LogPath);
            else
                await AddTextAsync(archive, "uptime.log", "No application log has been written yet.\r\n");
            await AddTextAsync(archive, "windows-events.txt", WindowsEventLogService.ReadDiagnosticEvents(DateTime.Now.AddDays(-7)));
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(destination);
    }

    private static async Task AddTextAsync(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(text);
    }

    private static async Task AddFileAsync(ZipArchive archive, string name, string path)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var output = entry.Open();
        await using var input = File.OpenRead(path);
        await input.CopyToAsync(output);
    }
}
