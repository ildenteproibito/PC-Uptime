using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public Localizer L => Localizer.Instance;
    public bool IsItalian => L.Language == "it";
    public bool IsEnglish => L.Language == "en";
    public double LanguageIndicatorOffset => IsEnglish ? 30 : 0;
    public MonitorViewModel Monitor { get; }
    public StabilityViewModel Stability { get; }
    public OperationsViewModel Operations { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonitor), nameof(IsStability), nameof(IsOperations), nameof(IsSettings))]
    private string _page = "Monitor";

    public bool IsMonitor => Page == "Monitor";
    public bool IsStability => Page == "Stability";
    public bool IsOperations => Page == "Operations";
    public bool IsSettings => Page == "Settings";

    public MainViewModel(IUptimeProvider uptime, IIdleProvider idle, IEventLogService eventLog,
        IPowerService power, IStartupService startup)
    {
        L.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Localizer.Language))
            {
                OnPropertyChanged(nameof(IsItalian));
                OnPropertyChanged(nameof(IsEnglish));
                OnPropertyChanged(nameof(LanguageIndicatorOffset));
                OnPropertyChanged(nameof(L));
            }
        };
        Settings = new SettingsViewModel();
        Monitor = new MonitorViewModel(uptime, idle, Settings);
        Stability = new StabilityViewModel(eventLog);
        Operations = new OperationsViewModel(power, startup);
    }

    [RelayCommand]
    private async Task Navigate(string page)
    {
        Page = page;
        if (page == "Stability") await Stability.LoadAsync();
    }

    [RelayCommand] private void SetLanguage(string code)
    {
        if (code is "it" or "en") L.Language = code;
    }
    [RelayCommand] private void OpenSettings() => Page = "Settings";
}
