using System;
using System.Linq;
using Avalonia;

namespace Uptime.Installer;

internal static class Program
{
    public static bool IsUninstallMode { get; private set; }
    public static string? UninstallTargetDir { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        IsUninstallMode = args.Any(a => string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase));
        var dirArg = args.FirstOrDefault(a => a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase));
        if (dirArg is not null)
        {
            UninstallTargetDir = dirArg.Substring("--dir=".Length).Trim('"');
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
