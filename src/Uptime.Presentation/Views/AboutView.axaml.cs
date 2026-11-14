using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Uptime.Presentation.Services;

namespace Uptime.Presentation.Views;

public partial class AboutView : UserControl
{
    public AboutView()
    {
        InitializeComponent();
        VersionBadgeText.Text = $"v{AppUpdateService.CurrentVersion}";
    }

    private void OnOpenWebsiteClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://uptime-pc.vercel.app/");

    private void OnOpenWallpapersClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://uptime-pc.vercel.app/wallpapers/");

    private void OnOpenDiscordClick(object? sender, RoutedEventArgs e) =>
        OpenUrl("https://discord.gg/88qrhwjBDP");

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore OS shell failures if no default browser is registered.
        }
    }
}
