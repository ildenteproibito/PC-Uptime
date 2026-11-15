using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Services;
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
    private readonly DiscordRpcService _discordRpc = new();
    private readonly long _rpcStartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    private DateTime _lastRpcUpdateUtc = DateTime.MinValue;
    private bool _rpcUpdateInFlight;

    public Localizer L => Localizer.Instance;
    public bool IsItalian => L.Language == "it";
    public bool IsEnglish => L.Language == "en";
    public bool IsRussian => L.Language == "ru";
    public double LanguageIndicatorOffset => IsEnglish ? 30 : IsRussian ? 60 : 0;
    public MonitorViewModel Monitor { get; }
    public StabilityViewModel Stability { get; }
    public OperationsViewModel Operations { get; }
    public SettingsViewModel Settings { get; }
    public AssistantViewModel Assistant { get; }
    public SpotifyViewModel? Spotify { get; }

    [ObservableProperty] private bool _isWidgetVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMonitor), nameof(IsStability), nameof(IsOperations), nameof(IsWallpapers), nameof(IsSettings), nameof(IsAbout), nameof(IsAssistant))]
    private string _page = "Monitor";

    private static readonly (string Page, string TitleKey, string BodyKey, string BubbleKey)[] TutorialSteps =
    [
        ("Monitor", "TutStep1Title", "TutStep1Body", "TutStep1Bubble"),
        ("Monitor", "TutStep2Title", "TutStep2Body", "TutStep2Bubble"),
        ("Stability", "TutStep3Title", "TutStep3Body", "TutStep3Bubble"),
        ("Operations", "TutStep4Title", "TutStep4Body", "TutStep4Bubble"),
        ("Wallpapers", "TutStep5Title", "TutStep5Body", "TutStep5Bubble"),
        ("Assistant", "TutStep6Title", "TutStep6Body", "TutStep6Bubble"),
        ("Settings", "TutStep7Title", "TutStep7Body", "TutStep7Bubble"),
    ];

    [ObservableProperty]
    private bool _isTutorialActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TutorialStepCounter), nameof(TutorialTitle), nameof(TutorialBody), nameof(CanTutorialGoBack), nameof(TutorialPrimaryButtonText))]
    private int _tutorialStepIndex;

    public string TutorialStepCounter => $"{TutorialStepIndex + 1} / {TutorialSteps.Length}";
    public string TutorialTitle => L[TutorialSteps[Math.Clamp(TutorialStepIndex, 0, TutorialSteps.Length - 1)].TitleKey];
    public string TutorialBody => L[TutorialSteps[Math.Clamp(TutorialStepIndex, 0, TutorialSteps.Length - 1)].BodyKey];
    public bool CanTutorialGoBack => TutorialStepIndex > 0;
    public string TutorialPrimaryButtonText => L[TutorialStepIndex >= TutorialSteps.Length - 1 ? "TutFinish" : "TutNext"];

    public bool IsMonitor => Page == "Monitor";
    public bool IsStability => Page == "Stability";
    public bool IsOperations => Page == "Operations";
    public bool IsWallpapers => Page == "Wallpapers";
    public bool IsSettings => Page == "Settings";
    public bool IsAbout => Page == "About";
    public bool IsAssistant => Page == "Assistant";
    public bool IsWallpaperFocusActive => IsMonitor && Monitor.IsWallpaperFocus;
    public bool ShowChromeOverlay => !IsWallpaperFocusActive;
    public bool ShowMascotOverlay => Monitor.ShowMascot && !IsWallpaperFocusActive;

    private static readonly string LiveStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "state.json");

    private readonly IUptimeProvider _uptime;
    private readonly IIdleProvider _idle;
    private readonly IBatteryService? _battery;
    private readonly DispatcherTimer _bridgeTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    public MainViewModel(
        IUptimeProvider uptime,
        IIdleProvider idle,
        IEventLogService eventLog,
        IPowerService power,
        IStartupService startup,
        IBatteryService? battery = null,
        ISleepBlockerService? sleepBlocker = null,
        IStartupAppsService? startupApps = null,
        ISpotifyMediaService? mediaService = null,
        IKeepAwakeService? keepAwake = null,
        IPowerPlanService? powerPlan = null,
        IWorkspaceRestoreService? workspaceRestore = null,
        IAppCrashService? appCrashes = null,
        IMemoryService? memory = null,
        INetworkWatchdogService? networkWatchdog = null,
        ICommunityWallpaperService? communityWallpapers = null)
    {
        _uptime = uptime;
        _idle = idle;
        _battery = battery;

        L.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Localizer.Language))
            {
                OnPropertyChanged(nameof(IsItalian));
                OnPropertyChanged(nameof(IsEnglish));
                OnPropertyChanged(nameof(IsRussian));
                OnPropertyChanged(nameof(LanguageIndicatorOffset));
                OnPropertyChanged(nameof(L));
                OnPropertyChanged(nameof(TutorialTitle));
                OnPropertyChanged(nameof(TutorialBody));
                OnPropertyChanged(nameof(TutorialPrimaryButtonText));
                RefreshDiscordRpcStatusText();
                TriggerImmediateDiscordRpcUpdate();
            }
        };
        Settings = new SettingsViewModel(communityWallpapers);
        Spotify = mediaService != null ? new SpotifyViewModel(mediaService) : null;
        Monitor = new MonitorViewModel(uptime, idle, Settings, Spotify, keepAwake);
        Stability = new StabilityViewModel(eventLog, appCrashes, networkWatchdog: networkWatchdog);
        Operations = new OperationsViewModel(power, startup, battery, sleepBlocker, startupApps, keepAwake, powerPlan, workspaceRestore, idle, Settings, memory);
        Assistant = new AssistantViewModel(uptime, idle, battery, Operations);

        Monitor.RequestIdleWindowOpen = OpenIdleWindow;
        Monitor.RequestIdleWindowClose = CloseIdleWindow;
        Monitor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(MonitorViewModel.IsWallpaperFocus) or nameof(MonitorViewModel.ShowMascot))
            {
                OnPropertyChanged(nameof(IsWallpaperFocusActive));
                OnPropertyChanged(nameof(ShowChromeOverlay));
                OnPropertyChanged(nameof(ShowMascotOverlay));
            }
        };
        Operations.OnMemoryTrimmed = mb => Monitor.FeedRamSnack(mb);
        Settings.RequestReplayTutorial = StartTutorial;
        Settings.RequestDiscordRpcRefresh = TriggerImmediateDiscordRpcUpdate;
        _discordRpc.StateChanged += () => Dispatcher.UIThread.Post(RefreshDiscordRpcStatusText);

        if (Spotify != null)
        {
            Spotify.PropertyChanged += OnSpotifyPropertyChanged;
            CheckSpotifyPopup();
        }

        _bridgeTimer.Tick += (_, _) =>
        {
            WriteLiveStateIfEnabled();
            if ((DateTime.UtcNow - _lastRpcUpdateUtc).TotalSeconds >= 12)
            {
                _ = UpdateDiscordPresenceAsync();
            }
        };
        _bridgeTimer.Start();
        RefreshDiscordRpcStatusText();
        _ = UpdateDiscordPresenceAsync();

        // Preload community wallpaper thumbnails in the background shortly after startup
        _ = PreloadCommunityWallpapersAsync();
        _ = CheckUpdatesOnStartupAsync();
        _ = LoadBetaTestersAsync();
        if (!Settings.HasCompletedTutorial)
        {
            _ = StartTutorialDelayedAsync();
        }
    }

    private async Task CheckUpdatesOnStartupAsync()
    {
        try
        {
            await Task.Delay(2500);
            await Settings.CheckForUpdatesSilentAsync();
        }
        catch
        {
            // Ignore silent startup check failures
        }
    }

    private async Task PreloadCommunityWallpapersAsync()
    {
        try
        {
            await Task.Delay(700);
            if (Settings.CommunityWallpapers.Count == 0)
            {
                await Settings.RefreshCommunityWallpapersCommand.ExecuteAsync(null);
            }
        }
        catch
        {
            // Best-effort startup preload
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
        _activeIdleWindow = new IdleWidgetWindow
        {
            DataContext = Monitor,
            AutoCloseOnActivity = true,
            BatterySummaryProvider = GetStandByBatterySummary
        };
        _activeIdleWindow.SetFullscreen(true);
        _activeIdleWindow.Closed += (_, _) => _activeIdleWindow = null;
        _activeIdleWindow.Show();
    }

    public void OpenStandByMode()
    {
        if (_activeIdleWindow is { IsVisible: true })
        {
            _activeIdleWindow.AutoCloseOnActivity = false;
            _activeIdleWindow.Activate();
            return;
        }
        _activeIdleWindow = new IdleWidgetWindow
        {
            DataContext = Monitor,
            AutoCloseOnActivity = false,
            BatterySummaryProvider = GetStandByBatterySummary
        };
        _activeIdleWindow.Closed += (_, _) => _activeIdleWindow = null;
        _activeIdleWindow.Show();
    }

    private string? GetStandByBatterySummary()
    {
        if (_battery == null) return null;
        var bat = _battery.GetBatteryStatus();
        if (!bat.HasBattery) return null;
        return bat.IsCharging ? $"{bat.Percent}% ⚡" : $"{bat.Percent}%";
    }

    private void CloseIdleWindow()
    {
        if (_activeIdleWindow is { AutoCloseOnActivity: true })
        {
            _activeIdleWindow.Close();
            _activeIdleWindow = null;
        }
    }

    private void WriteLiveStateIfEnabled()
    {
        if (!Settings.LiveStateBridge) return;
        try
        {
            var up = _uptime.GetUptime();
            var idle = _idle.GetIdleTime();
            var bat = _battery?.GetBatteryStatus();
            var spotifyTitle = Spotify?.HasTrack == true ? Spotify.Title : "";
            var spotifyArtist = Spotify?.HasTrack == true ? Spotify.Artist : "";
            var payload = new Dictionary<string, object>
            {
                ["uptimeSeconds"] = (long)up.TotalSeconds,
                ["idleSeconds"] = (long)idle.TotalSeconds,
                ["health"] = Health.For(up).ToString(),
                ["isKeepAwakeActive"] = Operations.IsKeepAwakeActuallyActive,
                ["powerPlan"] = Operations.ActivePowerPlan.ToString(),
                ["hasBattery"] = bat?.HasBattery ?? false,
                ["batteryPercent"] = bat?.Percent ?? 0,
                ["isCharging"] = bat?.IsCharging ?? false,
                ["spotifyTitle"] = spotifyTitle,
                ["spotifyArtist"] = spotifyArtist,
                ["spotifyIsPlaying"] = Spotify?.IsPlaying ?? false,
                ["updatedAt"] = DateTime.UtcNow.ToString("o")
            };
            var dir = Path.GetDirectoryName(LiveStatePath)!;
            Directory.CreateDirectory(dir);
            var tmp = LiveStatePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, LiveStatePath, true);

            var uptimeFormatted = up.TotalDays >= 1
                ? $"{(int)up.TotalDays} d {up.Hours} h {up.Minutes:00} m"
                : up.TotalHours >= 1
                    ? $"{up.Hours} h {up.Minutes:00} m {up.Seconds:00} s"
                    : $"{up.Minutes} m {up.Seconds:00} s";
            var cleanTitle = (spotifyTitle ?? "").Replace("\r", " ").Replace("\n", " ");
            var cleanArtist = (spotifyArtist ?? "").Replace("\r", " ").Replace("\n", " ");
            var spotifyTrack = !string.IsNullOrWhiteSpace(cleanTitle)
                ? (!string.IsNullOrWhiteSpace(cleanArtist) ? $"{cleanArtist} - {cleanTitle}" : cleanTitle)
                : "";

            var iniContent = string.Join("\r\n",
                "[Uptime]",
                $"UptimeFormatted={uptimeFormatted}",
                $"UptimeSeconds={payload["uptimeSeconds"]}",
                $"IdleSeconds={payload["idleSeconds"]}",
                $"Health={payload["health"]}",
                $"IsKeepAwakeActive={((bool)payload["isKeepAwakeActive"] ? "true" : "false")}",
                $"PowerPlan={payload["powerPlan"]}",
                $"HasBattery={((bool)payload["hasBattery"] ? "true" : "false")}",
                $"BatteryPercent={payload["batteryPercent"]}",
                $"IsCharging={((bool)payload["isCharging"] ? "true" : "false")}",
                $"SpotifyTitle={cleanTitle}",
                $"SpotifyArtist={cleanArtist}",
                $"SpotifyTrack={spotifyTrack}",
                $"SpotifyIsPlaying={((bool)payload["spotifyIsPlaying"] ? "true" : "false")}",
                $"UpdatedAt={payload["updatedAt"]}",
                "");
            var iniPath = Path.Combine(dir, "state.ini");
            var iniTmp = iniPath + ".tmp";
            File.WriteAllText(iniTmp, iniContent, new System.Text.UTF8Encoding(false));
            File.Move(iniTmp, iniPath, true);
        }
        catch
        {
            // Silent fail for optional bridge
        }
    }

    [RelayCommand]
    private async Task Navigate(string page)
    {
        Page = page;
        if (page == "Stability")
        {
            if (!IsTutorialActive) Monitor.NotifyDetectiveMode();
            await Stability.LoadAsync();
        }
        else if (page == "Wallpapers" && Settings.CommunityWallpapers.Count == 0)
            await Settings.RefreshCommunityWallpapersCommand.ExecuteAsync(null);
    }

    [RelayCommand] private void SetLanguage(string code)
    {
        if (code is "it" or "en" or "ru") L.Language = code;
    }
    [RelayCommand] private void OpenSettings() => Page = "Settings";
    [RelayCommand] private void OpenAbout()
    {
        Page = "About";
        _ = LoadBetaTestersAsync();
    }
    [RelayCommand] private void OpenAssistant() => Page = "Assistant";

    public void StartTutorial()
    {
        TutorialStepIndex = 0;
        IsTutorialActive = true;
        ApplyTutorialStep();
    }

    private async Task StartTutorialDelayedAsync()
    {
        try
        {
            await Task.Delay(500);
            Dispatcher.UIThread.Post(() =>
            {
                if (!Settings.HasCompletedTutorial && !IsTutorialActive)
                {
                    StartTutorial();
                }
            });
        }
        catch
        {
            // Ignore startup delay issues
        }
    }

    private void ApplyTutorialStep()
    {
        var step = TutorialSteps[Math.Clamp(TutorialStepIndex, 0, TutorialSteps.Length - 1)];
        _ = Navigate(step.Page);
        Monitor.ShowTutorialReaction(L[step.BubbleKey]);
    }

    [RelayCommand]
    private void NextTutorialStep()
    {
        if (TutorialStepIndex < TutorialSteps.Length - 1)
        {
            TutorialStepIndex++;
            ApplyTutorialStep();
        }
        else
        {
            CompleteTutorial();
        }
    }

    [RelayCommand]
    private void PreviousTutorialStep()
    {
        if (TutorialStepIndex > 0)
        {
            TutorialStepIndex--;
            ApplyTutorialStep();
        }
    }

    [RelayCommand]
    private void SkipTutorial() => CompleteTutorial();

    private void CompleteTutorial()
    {
        IsTutorialActive = false;
        Settings.MarkTutorialCompleted();
        Page = "Monitor";
    }

    partial void OnPageChanged(string value)
    {
        if (value != "Monitor" && Monitor.IsWallpaperFocus)
            Monitor.IsWallpaperFocus = false;
        if (value == "About")
            _ = LoadBetaTestersAsync();
        OnPropertyChanged(nameof(IsWallpaperFocusActive));
        OnPropertyChanged(nameof(ShowChromeOverlay));
        OnPropertyChanged(nameof(ShowMascotOverlay));
        TriggerImmediateDiscordRpcUpdate();
    }
    partial void OnIsTutorialActiveChanged(bool value) => TriggerImmediateDiscordRpcUpdate();

    private void TriggerImmediateDiscordRpcUpdate()
    {
        _lastRpcUpdateUtc = DateTime.MinValue;
        _ = UpdateDiscordPresenceAsync();
    }

    private void RefreshDiscordRpcStatusText()
    {
        if (Settings is null) return;
        if (!Settings.DiscordRichPresenceEnabled)
        {
            Settings.DiscordRpcStatus = L["DiscordStatusDisabled"];
            return;
        }

        if (_discordRpc.IsConnected && string.IsNullOrEmpty(_discordRpc.LastError))
        {
            var userLabel = string.IsNullOrWhiteSpace(_discordRpc.ConnectedUsername)
                ? "Discord"
                : _discordRpc.ConnectedUsername;
            Settings.DiscordRpcStatus = L.Format("DiscordStatusConnected", userLabel);
            return;
        }

        Settings.DiscordRpcStatus = _discordRpc.LastError switch
        {
            "MissingAppId" => L["DiscordStatusMissingId"],
            "InvalidAppId" => L["DiscordStatusInvalidId"],
            _ => L["DiscordStatusWaiting"]
        };
    }

    private async Task UpdateDiscordPresenceAsync()
    {
        if (_rpcUpdateInFlight || Settings is null) return;
        _rpcUpdateInFlight = true;
        _lastRpcUpdateUtc = DateTime.UtcNow;

        try
        {
            if (!Settings.DiscordRichPresenceEnabled)
            {
                await _discordRpc.ClearPresenceAsync();
                return;
            }

            var details = IsTutorialActive
                ? L["RpcTutorial"]
                : Settings.IsStudioOpen
                    ? L["RpcStudio"]
                    : Page switch
                    {
                        "Monitor" => L["RpcMonitor"],
                        "Stability" => L["RpcStability"],
                        "Operations" => L["RpcOperations"],
                        "Wallpapers" => L["RpcWallpapers"],
                        "Assistant" => L["RpcAssistant"],
                        "Settings" => L["RpcSettings"],
                        "About" => L["RpcAbout"],
                        _ => "Uptime"
                    };

            var up = _uptime.GetUptime();
            var upText = up.TotalDays >= 1
                ? $"{(int)up.TotalDays}d {up.Hours}h {up.Minutes:D2}m"
                : $"{(int)up.TotalHours}h {up.Minutes:D2}m";

            var uppyTag = string.IsNullOrEmpty(Monitor.MascotAccessoryBadge)
                ? $"Uppy ({Monitor.MascotStatusText})"
                : $"Uppy {Monitor.MascotAccessoryBadge}";

            var state = Operations.IsKeepAwakeActuallyActive
                ? $"Uptime: {upText} • Keep Awake • {uppyTag}"
                : Spotify is { HasTrack: true, IsPlaying: true } && !string.IsNullOrWhiteSpace(Spotify.Title)
                    ? $"Uptime: {upText} • {Spotify.Title} • {uppyTag}"
                    : $"Uptime: {upText} • {uppyTag}";

            await _discordRpc.UpdatePresenceAsync(
                Settings.DiscordApplicationId,
                details,
                state,
                _rpcStartUnix);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Discord RPC update failed.", ex);
        }
        finally
        {
            _rpcUpdateInFlight = false;
        }
    }

    public void DisposeDiscordRpc() => _discordRpc.Dispose();

    public void Dispose()
    {
        _bridgeTimer.Stop();
        if (Spotify is not null)
        {
            Spotify.PropertyChanged -= OnSpotifyPropertyChanged;
            Spotify.Dispose();
        }
        Monitor.Dispose();
        _discordRpc.Dispose();
    }

    private static readonly HttpClient BetaTestersHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private bool _loadingBetaTesters;
    private string _mobileDevName = "Koi";
    private Bitmap? _mobileDevAvatar;
    public string MobileDevName { get => _mobileDevName; private set => SetProperty(ref _mobileDevName, value); }
    public Bitmap? MobileDevAvatar { get => _mobileDevAvatar; private set => SetProperty(ref _mobileDevAvatar, value); }
    public ObservableCollection<BetaTesterCard> BetaTesters { get; } = new();
    public bool HasBetaTesters => BetaTesters.Count > 0;

    private async Task LoadBetaTestersAsync()
    {
        if (_loadingBetaTesters) return;
        _loadingBetaTesters = true;
        try
        {
            var json = await BetaTestersHttp.GetStringAsync("https://uptime-pc.vercel.app/api/beta-testers");
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("developers", out var devArr) &&
                devArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var dev in devArr.EnumerateArray())
                {
                    var devName = dev.TryGetProperty("name", out var dnEl) ? dnEl.GetString() : null;
                    var devAvatarUrl = dev.TryGetProperty("avatarUrl", out var daEl) ? daEl.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(devName))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => MobileDevName = devName);
                    }
                    if (!string.IsNullOrWhiteSpace(devAvatarUrl))
                    {
                        _ = LoadMobileDevAvatarAsync(devAvatarUrl);
                    }
                    break;
                }
            }

            if (!doc.RootElement.TryGetProperty("testers", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return;

            var parsed = new List<BetaTesterCard>();
            foreach (var item in arr.EnumerateArray())
            {
                var name = item.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                var avatarUrl = item.TryGetProperty("avatarUrl", out var aEl) ? aEl.GetString() : null;
                var desc = item.TryGetProperty("description", out var dEl) ? dEl.GetString() : null;
                var isMod = item.TryGetProperty("isModerator", out var mEl) && mEl.ValueKind == JsonValueKind.True;
                if (string.IsNullOrWhiteSpace(name)) continue;

                parsed.Add(new BetaTesterCard
                {
                    Name = name,
                    AvatarUrl = avatarUrl ?? "",
                    IsModerator = isMod,
                    Description = string.IsNullOrWhiteSpace(desc) ? "Official Uptime Beta Tester" : desc
                });
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                BetaTesters.Clear();
                foreach (var t in parsed) BetaTesters.Add(t);
                OnPropertyChanged(nameof(HasBetaTesters));
            });

            foreach (var card in parsed)
            {
                _ = LoadTesterAvatarAsync(card);
            }
        }
        catch
        {
            // Best-effort network load
        }
        finally
        {
            _loadingBetaTesters = false;
        }
    }

    private static async Task LoadTesterAvatarAsync(BetaTesterCard card)
    {
        if (string.IsNullOrWhiteSpace(card.AvatarUrl)) return;
        try
        {
            var bytes = await BetaTestersHttp.GetByteArrayAsync(card.AvatarUrl);
            using var ms = new MemoryStream(bytes);
            var bmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 144));
            await Dispatcher.UIThread.InvokeAsync(() => card.AvatarBitmap = bmp);
        }
        catch
        {
            // Ignore single avatar failure
        }
    }

    private async Task LoadMobileDevAvatarAsync(string url)
    {
        try
        {
            var bytes = await BetaTestersHttp.GetByteArrayAsync(url);
            using var ms = new MemoryStream(bytes);
            var bmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 144));
            await Dispatcher.UIThread.InvokeAsync(() => MobileDevAvatar = bmp);
        }
        catch
        {
            // Ignore avatar failure
        }
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public partial class BetaTesterCard : ObservableObject
{
    public string Name { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public bool IsModerator { get; init; }
    public string Description { get; init; } = "";
    [ObservableProperty] private Bitmap? _avatarBitmap;
}
