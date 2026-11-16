using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class NewsView : UserControl
{
    public NewsView()
    {
        InitializeComponent();
    }

    private async void OnPickCoverImageClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is NewsViewModel vm && TopLevel.GetTopLevel(this) is { } top)
        {
            try
            {
                var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Seleziona Immagine Thumbnail / Cover",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Immagini (*.png; *.jpg; *.jpeg; *.webp)")
                        {
                            Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" }
                        }
                    }
                });

                if (files is { Count: > 0 } && files[0] is { } file)
                {
                    var localPath = file.TryGetLocalPath();
                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        await vm.SetCoverImageFromLocalPathAsync(localPath);
                    }
                }
            }
            catch
            {
                // Ignore storage picker cancellation
            }
        }
    }
}
