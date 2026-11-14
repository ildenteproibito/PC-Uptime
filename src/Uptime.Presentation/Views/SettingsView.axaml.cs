using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private void OnScrollThemesLeftClick(object? sender, RoutedEventArgs e)
    {
        var sv = this.FindControl<ScrollViewer>("ThemesScrollViewer");
        if (sv is not null)
        {
            sv.Offset = new Avalonia.Vector(Math.Max(0, sv.Offset.X - 240), 0);
        }
    }

    private void OnScrollThemesRightClick(object? sender, RoutedEventArgs e)
    {
        var sv = this.FindControl<ScrollViewer>("ThemesScrollViewer");
        if (sv is not null)
        {
            sv.Offset = new Avalonia.Vector(sv.Offset.X + 240, 0);
        }
    }

    private async void OnPickCustomWallpaperClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = Localizer.Instance["PickWallpaperTitle"],
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(Localizer.Instance["PickWallpaperFilter"])
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                    }
                }
            });

            if (files is { Count: > 0 } && files[0] is { } chosen)
            {
                var localPath = chosen.TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(localPath) && System.IO.File.Exists(localPath))
                {
                    vm.SetCustomBackground(localPath);
                }
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to pick custom wallpaper", ex);
        }
    }

    private void OnClearCustomWallpaperClick(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (DataContext is SettingsViewModel vm)
        {
            vm.ClearCustomBackground();
        }
    }

    private async void OnCreateDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || TopLevel.GetTopLevel(this) is not { } top) return;
        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"uptime-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
                FileTypeChoices = new[] { new FilePickerFileType("ZIP archive") { Patterns = new[] { "*.zip" } } }
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            await DiagnosticArchive.WriteAsync(stream);
            vm.DiagnosticsStatus = Localizer.Instance["DiagnosticsReady"];
            AppDiagnostics.Write("Information", "Diagnostic archive created.");
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to create diagnostic archive.", ex);
            vm.DiagnosticsStatus = Localizer.Instance["DiagnosticsFailed"];
        }
    }

    private async void OnCopyLiveStatePathClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel vm || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try
        {
            await clipboard.SetTextAsync(vm.LiveStateFilePath);
            vm.BridgeActionStatus = Localizer.Instance["BridgePathCopied"];
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to copy Live State path to clipboard.", ex);
        }
    }

    private void OnBugReportToggleClick(object? sender, RoutedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is SettingsViewModel { IsBugReportOpen: true })
            {
                SettingsScrollViewer?.ScrollToEnd();
                BugReportTitleBox?.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }
}
