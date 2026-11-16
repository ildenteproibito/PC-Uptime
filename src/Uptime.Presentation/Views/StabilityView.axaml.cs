using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Views;

public partial class StabilityView : UserControl
{
    public StabilityView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is StabilityViewModel vm)
            {
                vm.RequestScrollToFixes = () => CommunityFixesCard.BringIntoView();
            }
        };
    }

    private void OnFixSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is StabilityViewModel vm)
        {
            e.Handled = true;
            vm.SearchFixesCommand.Execute(null);
        }
    }
    private async void OnExportJsonClick(object? sender, RoutedEventArgs e) => await ExportAsync("json");
    private async void OnExportCsvClick(object? sender, RoutedEventArgs e) => await ExportAsync("csv");
    private async Task ExportAsync(string format)
    {
        var top = TopLevel.GetTopLevel(this);
        if (DataContext is not StabilityViewModel vm || top is null) return;
        try
        {
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"uptime-stability.{format}",
                FileTypeChoices = new[] { new FilePickerFileType(format.ToUpperInvariant()) { Patterns = new[] { $"*.{format}" } } }
            });
            if (file is null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(format == "json" ? vm.ExportJson() : vm.ExportCsv());
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", $"Failed to export stability data as {format}", ex);
        }
    }
}
