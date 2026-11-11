using System.ComponentModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Views;

namespace Uptime.Presentation.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private MiniHudWindow? _activeHudWindow;
    private IdleWidgetWindow? _activeIdleWindow;
    private SpotifyWidgetWindow? _activeSpotifyWindow;
    private bool _spotifyDismissedByUser;
    private string _lastSpotifyTitle = "";
    private bool _wasPlaying;
    private double _lastTrackSec;

    public Localizer L => Localizer.Instance;
    public bool IsItalian => L.Language == "it";
    public bool IsEnglish => L.Language == "en";
    public double LanguageIndicatorOffset => IsEnglish ? 30 : 0;
    public MonitorViewModel Monitor { get; }
    public StabilityViewModel Stability { get; }
    public OperationsViewModel Operations { get; }
    public SettingsViewModel Settings { get; }
    public SpotifyViewModel? Spotify { get; }

    [ObservableProperty] private bool _isWidgetVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonitor), nameof(IsStability), nameof(IsOperations), nameof(IsSettings))]
    private string _page = "Monitor";

    public bool IsMonitor => Page == "Monitor";
    public bool IsStability => Page == "Stability";
    public bool IsOperations => Page == "Operations";
    public bool IsSettings => Page == "Settings";

    public MainViewModel(
        IUptimeProvider uptime,
        IIdleProvider idle,
        IEventLogService eventLog,
        IPowerService power,
        IStartupService startup,
        IBatteryService? battery = null,
        ISleepBlockerService? sleepBlocker = null,
        IStartupAppsService? startupApps = null,
        ISpotifyMediaService? mediaService = null)
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
        Spotify = mediaService != null ? new SpotifyViewModel(mediaService) : null;
        Monitor = new MonitorViewModel(uptime, idle, Settings, Spotify);
        Stability = new StabilityViewModel(eventLog);
        Operations = new OperationsViewModel(power, startup, battery, sleepBlocker, startupApps);

        Monitor.RequestIdleWindowOpen = OpenIdleWindow;
        Monitor.RequestIdleWindowClose = CloseIdleWindow;

        if (Spotify != null)
        {
            Spotify.PropertyChanged += OnSpotifyPropertyChanged;
            CheckSpotifyPopup();
        }
    }

    private void OnSpotifyPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Spotify == null) return;
        if (e.PropertyName == nameof(SpotifyViewModel.Title))
        {
            if (Spotify.Title != _lastSpotifyTitle)
            {
                _lastSpotifyTitle = Spotify.Title;
                _spotifyDismissedByUser = false;
            }
        }
        else if (e.PropertyName == nameof(SpotifyViewModel.IsPlaying))
        {
            // When playback resumes or restarts, reset the dismiss flag
            if (Spotify.IsPlaying && !_wasPlaying)
            {
                _spotifyDismissedByUser = false;
            }
            _wasPlaying = Spotify.IsPlaying;
        }
        else if (e.PropertyName == nameof(SpotifyViewModel.ProgressValue))
        {
            // If the track restarts from the beginning or loops (position resets < 2.5s from > 5s)
            if (Spotify.ProgressValue < 2.5 && _lastTrackSec > 5.0)
            {
                _spotifyDismissedByUser = false;
            }
            _lastTrackSec = Spotify.ProgressValue;
        }

        if (e.PropertyName is nameof(SpotifyViewModel.HasTrack) or nameof(SpotifyViewModel.IsPlaying) or nameof(SpotifyViewModel.Title) or nameof(SpotifyViewModel.ProgressValue))
        {
            Dispatcher.UIThread.Post(CheckSpotifyPopup);
        }
    }

    private void CheckSpotifyPopup()
    {
        if (Spotify == null) return;

        if (Spotify.HasTrack && Spotify.IsPlaying)
        {
            if (_activeSpotifyWindow == null && !_spotifyDismissedByUser)
            {
                _activeSpotifyWindow = new SpotifyWidgetWindow { DataContext = Spotify };
                _activeSpotifyWindow.UserDismissed += () => _spotifyDismissedByUser = true;
                _activeSpotifyWindow.Closed += (_, _) => _activeSpotifyWindow = null;
                _activeSpotifyWindow.Show();
            }
        }
        else if (!Spotify.HasTrack)
        {
            _activeSpotifyWindow?.Close();
            _activeSpotifyWindow = null;
            _spotifyDismissedByUser = false;
        }
    }

    [RelayCommand]
    public void ToggleSpotifyWidget()
    {
        if (Spotify == null) return;
        if (_activeSpotifyWindow is { IsVisible: true })
        {
            _spotifyDismissedByUser = true;
            _activeSpotifyWindow.Close();
            _activeSpotifyWindow = null;
        }
        else if (Spotify.HasTrack)
        {
            _spotifyDismissedByUser = false;
            _activeSpotifyWindow = new SpotifyWidgetWindow { DataContext = Spotify };
            _activeSpotifyWindow.UserDismissed += () => _spotifyDismissedByUser = true;
            _activeSpotifyWindow.Closed += (_, _) => _activeSpotifyWindow = null;
            _activeSpotifyWindow.Show();
        }
    }

    [RelayCommand]
    public void ToggleDesktopWidget()
    {
        if (_activeHudWindow is { IsVisible: true })
        {
            _activeHudWindow.Hide();
            IsWidgetVisible = false;
        }
        else
        {
            if (_activeHudWindow == null)
            {
                _activeHudWindow = new MiniHudWindow { DataContext = Monitor };
                _activeHudWindow.Closed += (_, _) =>
                {
                    _activeHudWindow = null;
                    IsWidgetVisible = false;
                };
            }
            _activeHudWindow.Show();
            _activeHudWindow.Activate();
            IsWidgetVisible = true;
        }
    }

    private void OpenIdleWindow()
    {
        if (_activeIdleWindow is { IsVisible: true }) return;
        _activeIdleWindow = new IdleWidgetWindow { DataContext = Monitor };
        _activeIdleWindow.Closed += (_, _) => _activeIdleWindow = null;
        _activeIdleWindow.Show();
    }

    private void CloseIdleWindow()
    {
        _activeIdleWindow?.Close();
        _activeIdleWindow = null;
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
