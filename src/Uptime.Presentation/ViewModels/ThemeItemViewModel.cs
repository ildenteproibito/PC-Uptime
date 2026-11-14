using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Presentation.Services;

namespace Uptime.Presentation.ViewModels;

public partial class ThemeItemViewModel : ObservableObject
{
    public ThemeDefinition Definition { get; }
    public string Id => Definition.Id;

    public IBrush AccentBrush { get; }
    public IBrush SurfaceBrush { get; }
    public IBrush BgBrush { get; }
    public IBrush TextBrush { get; }

    [ObservableProperty] private bool _isSelected;

    public Action<ThemeItemViewModel>? OnSelect { get; set; }

    public ThemeItemViewModel(ThemeDefinition definition, bool isSelected, Action<ThemeItemViewModel>? onSelect = null)
    {
        Definition = definition;
        _isSelected = isSelected;
        OnSelect = onSelect;
        AccentBrush = new SolidColorBrush(Color.Parse(definition.AccentColor));
        SurfaceBrush = new SolidColorBrush(Color.Parse(definition.SurfaceColor));
        BgBrush = new SolidColorBrush(Color.Parse(definition.BgColor));
        TextBrush = new SolidColorBrush(Color.Parse(definition.TextColor));
    }

    [RelayCommand]
    private void Select() => OnSelect?.Invoke(this);

    public void RefreshLanguage() { }
}
