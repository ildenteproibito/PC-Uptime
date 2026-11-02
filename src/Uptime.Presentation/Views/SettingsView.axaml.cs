using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

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
}
