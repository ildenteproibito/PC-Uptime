using System.Text;

namespace Uptime.Presentation;

internal static class AppDiagnostics
{
    private static readonly object Gate = new();
    public static string LogDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "Logs");
    public static string LogPath => Path.Combine(LogDirectory, "uptime.log");

    public static void Write(string level, string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" [").Append(level).Append("] ").AppendLine(message);
            if (exception is not null) line.AppendLine(exception.ToString());
            lock (Gate) File.AppendAllText(LogPath, line.ToString(), Encoding.UTF8);
        }
        catch { /* Diagnostics must never prevent the app from running. */ }
    }
}
