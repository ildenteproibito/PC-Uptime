using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public partial class MonitorViewModel : ObservableObject, IDisposable
{
    private readonly IUptimeProvider _uptime;
    private readonly IIdleProvider _idle;
    private readonly SettingsViewModel _settings;
    private readonly IKeepAwakeService? _keepAwake;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, Bitmap?> _mascotCache = new();
    private string _currentMascotKey = "";
    private DateTime _bubbleExpiresAt = DateTime.MinValue;
    private int _pokeIndex;
    private static readonly string RecordPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "personal-record.txt");
    private double _personalBestSeconds;
    private double _lastSavedBestSeconds;
    private string? _lastRulerLang;
    private PropertyChangedEventHandler? _settingsChangedHandler;

    private static readonly IBrush Good = new SolidColorBrush(Color.Parse("#7BC4A8"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#E0A458"));
    private static readonly IBrush Orange = new SolidColorBrush(Color.Parse("#E8845A"));
    private static readonly IBrush IdleBlue = new SolidColorBrush(Color.Parse("#60A5FA"));

    public Localizer L => Localizer.Instance;
    public string OsText { get; } = RuntimeInformation.OSDescription;
    public string ThreadsText { get; } = Environment.ProcessorCount.ToString();

    [ObservableProperty] private string _primaryText = "";
    [ObservableProperty] private string _secondaryText = "";
    [ObservableProperty] private string _bootText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private IBrush _statusBrush = Good;
    [ObservableProperty] private string _adviceText = "";
    [ObservableProperty] private string _idleText = "";
    [ObservableProperty] private string _startedText = "";
    [ObservableProperty] private int _filledTicks;
    public int HudRulerTicks => Math.Clamp(FilledTicks / 4, 0, 42);
    partial void OnFilledTicksChanged(int value) => OnPropertyChanged(nameof(HudRulerTicks));
    [ObservableProperty] private IReadOnlyList<string> _rulerLabels = Array.Empty<string>();
    [ObservableProperty] private string _clipboardText = "";
    [ObservableProperty] private string _personalRecordText = "";
    [ObservableProperty] private string _hudThresholdText = "";
    [ObservableProperty] private Bitmap? _mascotImage;
    [ObservableProperty] private string _mascotStatusText = "";
    [ObservableProperty] private IBrush _mascotStatusBrush = Good;
    [ObservableProperty] private string _mascotBubbleText = "";
    [ObservableProperty] private bool _isMascotBubbleVisible;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMascotAccessory))]
    private string _mascotAccessoryBadge = "";

    public bool HasMascotAccessory => !string.IsNullOrEmpty(MascotAccessoryBadge);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUiVisible))]
    private bool _isWallpaperFocus;

    public bool IsUiVisible => !IsWallpaperFocus;

    [RelayCommand]
    private void ToggleWallpaperFocus() => IsWallpaperFocus = !IsWallpaperFocus;

    private DateTime _lastNightCommentUtc = DateTime.MinValue;
    private string _lastSpotifyAnnouncedTitle = "";
    private DateTime _lastPetUtc = DateTime.MinValue;
    private DateTime _lastBounceUtc = DateTime.MinValue;

    public bool ShowSession => _settings.ShowSession;
    public bool ShowAdvice => _settings.ShowAdvice;
    public bool ShowSystem => _settings.ShowSystem;
    public bool ShowMascot => _settings.ShowMascot;
    public string MascotMoodKey => _currentMascotKey;

    public SpotifyViewModel? Spotify { get; }
    public Action? RequestIdleWindowOpen { get; set; }
    public Action? RequestIdleWindowClose { get; set; }
    public event Action? RequestMascotCelebrate;
    public event Action? RequestMascotHearts;

    public MonitorViewModel(IUptimeProvider uptime, IIdleProvider idle, SettingsViewModel settings, SpotifyViewModel? spotify = null, IKeepAwakeService? keepAwake = null)
    {
        _uptime = uptime;
        _idle = idle;
        _settings = settings;
        _keepAwake = keepAwake;
        Spotify = spotify;
        try { _personalBestSeconds = double.Parse(File.ReadAllText(RecordPath), System.Globalization.CultureInfo.InvariantCulture); } catch (Exception e) when (e is IOException or FormatException or UnauthorizedAccessException) { }
        _lastSavedBestSeconds = _personalBestSeconds;
        _settingsChangedHandler = (_, _) =>
        {
            OnPropertyChanged(nameof(ShowSession));
            OnPropertyChanged(nameof(ShowAdvice));
            OnPropertyChanged(nameof(ShowSystem));
            OnPropertyChanged(nameof(ShowMascot));
        };
        _settings.PropertyChanged += _settingsChangedHandler;
        _settings.WallpaperChanged += OnWallpaperChanged;
        if (Spotify != null) Spotify.PropertyChanged += OnSpotifyChanged;
        Localizer.Instance.PropertyChanged += OnLanguageChanged;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        Refresh();
        _timer.Start();
    }

    private void OnSpotifyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SpotifyViewModel.IsPlaying) or nameof(SpotifyViewModel.HasTrack))
        {
            if (Dispatcher.UIThread.CheckAccess()) Refresh();
            else Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void OnLanguageChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(L));
        Refresh();
    }

    private void Refresh()
    {
        var l = Localizer.Instance;
        var up = _uptime.GetUptime();
        if (up.TotalSeconds > _personalBestSeconds)
        {
            _personalBestSeconds = up.TotalSeconds;
            if (_personalBestSeconds - _lastSavedBestSeconds >= 60)
            {
                _lastSavedBestSeconds = _personalBestSeconds;
                try { Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!); File.WriteAllText(RecordPath, _personalBestSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
        var p = UptimeParts.From(up);
        var boot = DateTime.Now - up;

        if (p.Days > 0)
        {
            PrimaryText = l.Plural(p.Days, "UnitDay");
            SecondaryText = $"{p.Hours:00} h  {p.Minutes:00} m  {p.Seconds:00} s";
        }
        else if (p.Hours > 0)
        {
            PrimaryText = $"{p.Hours} h";
            SecondaryText = $"{p.Minutes:00} m  {p.Seconds:00} s";
        }
        else
        {
            PrimaryText = $"{p.Minutes} m";
            SecondaryText = $"{p.Seconds:00} s";
        }

        var bootFormatted = boot.ToString("d MMM, HH:mm", l.Culture);
        BootText = l.Format("BootOn", bootFormatted);
        StartedText = bootFormatted;
        var best = TimeSpan.FromSeconds(_personalBestSeconds);
        PersonalRecordText = l.Format("PersonalRecord", (int)best.TotalDays, best.Hours);
        HudThresholdText = l.Format("RulerDay", 7);

        var level = Health.For(up);
        StatusText = l["Health" + level];
        StatusBrush = level switch { HealthLevel.Good => Good, HealthLevel.Attention => Amber, _ => Orange };
        AdviceText = level switch
        {
            HealthLevel.Good => l["AdviceGood"],
            HealthLevel.Attention => l.Format("AdviceAttention", Health.DaysUntilMaintenance(up)),
            _ => l["AdviceMaintenance"],
        };

        FilledTicks = (int)Math.Min(168, up.TotalHours);
        if (_lastRulerLang != l.Language)
        {
            _lastRulerLang = l.Language;
            var labels = new List<string> { l["RulerStart"] };
            for (var d = 1; d <= 7; d++) labels.Add(l.Format("RulerDay", d));
            RulerLabels = labels;
        }

        var idle = _idle.GetIdleTime();
        IdleText = $"{(int)idle.TotalMinutes} m {idle.Seconds:00} s";
        ClipboardText = l.Format("ClipboardText", $"{PrimaryText} {SecondaryText}", bootFormatted);

        UpdateMascot(l, level, idle, up);

        if (idle >= TimeSpan.FromMinutes(30))
        {
            RequestIdleWindowOpen?.Invoke();
        }
        else if (idle < TimeSpan.FromSeconds(5))
        {
            RequestIdleWindowClose?.Invoke();
        }
    }

    private void UpdateMascot(Localizer l, HealthLevel level, TimeSpan idle, TimeSpan up)
    {
        string key;
        string labelKey;
        IBrush brush;

        if (level == HealthLevel.Maintenance)
        {
            key = "critical";
            labelKey = "MascotCritical";
            brush = Orange;
        }
        else if (_keepAwake?.IsActive == true)
        {
            key = "coffee";
            labelKey = "MascotCoffee";
            brush = Good;
        }
        else if (Spotify is { HasTrack: true, IsPlaying: true })
        {
            key = "spotify";
            labelKey = "MascotSpotify";
            brush = Good;
        }
        else if (idle >= TimeSpan.FromMinutes(2))
        {
            key = "idle";
            labelKey = "MascotIdle";
            brush = IdleBlue;
        }
        else if (level == HealthLevel.Attention)
        {
            key = "attention";
            labelKey = "MascotAttention";
            brush = Amber;
        }
        else
        {
            key = "good";
            labelKey = "MascotGood";
            brush = Good;
        }

        MascotStatusText = l[labelKey];
        MascotStatusBrush = brush;

        if (_currentMascotKey != key)
        {
            var hadPrevious = !string.IsNullOrEmpty(_currentMascotKey);
            _currentMascotKey = key;
            MascotImage = GetOrLoadMascotBitmap(key);
            if (hadPrevious)
            {
                ShowMascotBubble(GetMoodThought(l, key), TimeSpan.FromSeconds(5));
            }
        }
        else if (IsMascotBubbleVisible && DateTime.UtcNow >= _bubbleExpiresAt)
        {
            IsMascotBubbleVisible = false;
        }

        MascotAccessoryBadge = Spotify is { HasTrack: true, IsPlaying: true }
            ? "🎵"
            : _keepAwake?.IsActive == true
                ? "☕"
                : up.TotalDays >= 7
                    ? "👑"
                    : up.TotalDays >= 3
                        ? "🔥"
                        : up.TotalDays >= 1
                            ? "😎"
                            : DateTime.Now.Hour is >= 0 and < 5
                                ? "🌙"
                                : "";

        if (Spotify is { HasTrack: true, IsPlaying: true } && !string.IsNullOrWhiteSpace(Spotify.Title))
        {
            if (!string.Equals(_lastSpotifyAnnouncedTitle, Spotify.Title, StringComparison.Ordinal))
            {
                _lastSpotifyAnnouncedTitle = Spotify.Title;
                if (!IsMascotBubbleVisible)
                {
                    ShowMascotBubble(l.Format("UppySpotifyVibe", Spotify.Title), TimeSpan.FromSeconds(4));
                }
            }
        }
        else if (DateTime.Now.Hour is >= 0 and < 5 && _settings.NightReminder && !IsMascotBubbleVisible &&
                 (DateTime.UtcNow - _lastNightCommentUtc).TotalMinutes >= 15)
        {
            _lastNightCommentUtc = DateTime.UtcNow;
            var nightKey = (DateTime.Now.Minute % 2 == 0) ? "UppyNight1" : "UppyNight2";
            ShowMascotBubble(l[nightKey], TimeSpan.FromSeconds(5.5));
        }
    }

    public void PetMascot()
    {
        if ((DateTime.UtcNow - _lastPetUtc).TotalSeconds < 4) return;
        _lastPetUtc = DateTime.UtcNow;
        var l = Localizer.Instance;
        var key = (_pokeIndex++ % 2 == 0) ? "UppyPet1" : "UppyPet2";
        ShowMascotBubble(l[key], TimeSpan.FromSeconds(3.5));
        RequestMascotCelebrate?.Invoke();
        RequestMascotHearts?.Invoke();
    }

    public void NotifyMascotBounce()
    {
        if ((DateTime.UtcNow - _lastBounceUtc).TotalSeconds < 3) return;
        _lastBounceUtc = DateTime.UtcNow;
        var l = Localizer.Instance;
        ShowMascotBubble(l["UppyBounce"], TimeSpan.FromSeconds(3.5));
        RequestMascotCelebrate?.Invoke();
    }

    public void FeedRamSnack(long freedMb)
    {
        if (!ShowMascot) return;
        var l = Localizer.Instance;
        ShowMascotBubble(l.Format("UppySnack", Math.Max(1, freedMb)), TimeSpan.FromSeconds(4.5));
        RequestMascotCelebrate?.Invoke();
    }

    public void NotifyDetectiveMode()
    {
        if (!ShowMascot || IsMascotBubbleVisible) return;
        ShowMascotBubble(Localizer.Instance["UppyDetective"], TimeSpan.FromSeconds(4));
    }

    public void PokeMascot()
    {
        var l = Localizer.Instance;
        var choices = new List<string>
        {
            GetMoodThought(l, _currentMascotKey),
            l["UppyPoke1"],
            l["UppyPoke2"],
            l["UppyPoke3"],
        };
        if (_settings.CurrentWallpaperContext is { Title.Length: > 0 } wp)
        {
            choices.Add(l.Format("UppyWallPoke", wp.Title));
        }
        var msg = choices[_pokeIndex % choices.Count];
        _pokeIndex++;
        ShowMascotBubble(msg, TimeSpan.FromSeconds(4.5));
    }

    private void OnWallpaperChanged(SettingsViewModel.WallpaperReactionContext ctx)
    {
        if (!ShowMascot) return;
        var l = Localizer.Instance;
        string comment;

        if (ctx.IsCleared)
        {
            comment = l["UppyWallCleared"];
        }
        else
        {
            var title = string.IsNullOrWhiteSpace(ctx.Title) ? "Wallpaper" : ctx.Title.Trim();
            var lower = title.ToLowerInvariant();
            if (lower.Contains("oled") || lower.Contains("dark") || lower.Contains("black") || lower.Contains("night") || lower.Contains("amoled"))
            {
                comment = l["UppyWallOled"];
            }
            else if (lower.Contains("spider") || lower.Contains("venom") || lower.Contains("batman") || lower.Contains("cyberpunk") || lower.Contains("hogwarts") || lower.Contains("game") || lower.Contains("marvel") || lower.Contains("valorant"))
            {
                comment = l.Format("UppyWallGaming", title);
            }
            else if (lower.Contains("neon") || lower.Contains("cyber") || lower.Contains("space") || lower.Contains("galaxy") || lower.Contains("sci-fi"))
            {
                comment = l.Format("UppyWallNeon", title);
            }
            else if (lower.Contains("forest") || lower.Contains("nature") || lower.Contains("mountain") || lower.Contains("sunset") || lower.Contains("ocean") || lower.Contains("chill"))
            {
                comment = l.Format("UppyWallNature", title);
            }
            else if (ctx.PixelWidth >= 3840 || (ctx.Resolution?.Contains("3840") ?? false))
            {
                comment = l.Format("UppyWall4K", title);
            }
            else if (ctx.IsCommunity && !string.IsNullOrWhiteSpace(ctx.Author))
            {
                comment = l.Format("UppyWallCommunity", title, ctx.Author);
            }
            else
            {
                comment = l.Format("UppyWallLocal", title);
            }
        }

        ShowMascotBubble(comment, TimeSpan.FromSeconds(5.5));
        RequestMascotCelebrate?.Invoke();
    }

    public void ShowTutorialReaction(string text)
    {
        if (!ShowMascot) return;
        ShowMascotBubble(text, TimeSpan.FromSeconds(6));
        RequestMascotCelebrate?.Invoke();
    }

    public void SetMascotDragging(bool dragging)
    {
        if (dragging)
        {
            ShowMascotBubble(Localizer.Instance["UppyDrag"], TimeSpan.FromSeconds(10));
        }
        else if (IsMascotBubbleVisible && MascotBubbleText == Localizer.Instance["UppyDrag"])
        {
            _bubbleExpiresAt = DateTime.UtcNow.AddSeconds(1.2);
        }
    }

    private void ShowMascotBubble(string text, TimeSpan duration)
    {
        MascotBubbleText = text;
        IsMascotBubbleVisible = !string.IsNullOrWhiteSpace(text);
        _bubbleExpiresAt = DateTime.UtcNow.Add(duration);
    }

    private static string GetMoodThought(Localizer l, string key) => key switch
    {
        "critical" => l["UppyMoodCritical"],
        "coffee" => l["UppyMoodCoffee"],
        "spotify" => l["UppyMoodSpotify"],
        "idle" => l["UppyMoodIdle"],
        "attention" => l["UppyMoodAttention"],
        _ => l["UppyMoodGood"],
    };

    private Bitmap? GetOrLoadMascotBitmap(string key)
    {
        if (_mascotCache.TryGetValue(key, out var cached)) return cached;
        Bitmap? bmp = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri($"avares://Uptime/Assets/Mascot/{key}.png"));
            bmp = new Bitmap(stream);
        }
        catch { }
        _mascotCache[key] = bmp;
        return bmp;
    }

    public void Dispose()
    {
        _timer.Stop();
        if (_settingsChangedHandler != null) _settings.PropertyChanged -= _settingsChangedHandler;
        _settings.WallpaperChanged -= OnWallpaperChanged;
        if (Spotify != null) Spotify.PropertyChanged -= OnSpotifyChanged;
        Localizer.Instance.PropertyChanged -= OnLanguageChanged;
        if (_personalBestSeconds > _lastSavedBestSeconds)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(RecordPath)!); File.WriteAllText(RecordPath, _personalBestSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        foreach (var bmp in _mascotCache.Values) bmp?.Dispose();
        _mascotCache.Clear();
    }
}
