using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class WallpapersView : UserControl
{
    public WallpapersView() => InitializeComponent();

    private async void OnPickCommunityImageClick(object? sender, RoutedEventArgs e)
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
                    vm.SetPendingUploadImage(localPath);
                }
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to pick wallpaper for upload", ex);
        }
    }
}
