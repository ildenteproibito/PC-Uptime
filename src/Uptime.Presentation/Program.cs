using Avalonia;

namespace Uptime.Presentation;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Istanza singola (FEAT-08)
        using var mutex = new Mutex(true, "Uptime_SingleInstance_Mutex", out var created);
        if (!created) return;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
