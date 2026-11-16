using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;
using Uptime.Presentation.Services;

namespace Uptime.Presentation.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime", "settings.json");
    public Localizer L => Localizer.Instance;
    public bool IsItalian => L.Language == "it";
    public bool IsEnglish => L.Language == "en";
    public bool IsRussian => L.Language == "ru";
    public double LanguageIndicatorOffset => IsEnglish ? 100 : IsRussian ? 200 : 0;
    [ObservableProperty] private bool _showSession = true;
    [ObservableProperty] private bool _showAdvice = true;
    [ObservableProperty] private bool _showSystem = true;
    [ObservableProperty] private bool _showMascot = true;
    [ObservableProperty] private bool _showGlassContainers = true;
    [ObservableProperty] private double? _mascotPosX;
    [ObservableProperty] private double? _mascotPosY;
    [ObservableProperty] private bool _nightReminder;
    [ObservableProperty] private bool _liveStateBridge;
    [ObservableProperty] private bool _restoreWorkspaceOnReboot;
    [ObservableProperty] private bool _powerPlanAutopilot;
    [ObservableProperty] private string _keepAwakeProcessName = "";
    [ObservableProperty] private bool _keepAwakeBatteryGuard = true;
    [ObservableProperty] private string _selectedPreset = "Default";
    [ObservableProperty] private string _selectedThemeId = ThemeManager.DefaultThemeId;
    [ObservableProperty] private string _diagnosticsStatus = "";
    [ObservableProperty] private bool _widgetIncludeUptime = true;
    [ObservableProperty] private bool _widgetIncludeHealth = true;
    [ObservableProperty] private bool _widgetIncludePower = true;
    [ObservableProperty] private bool _widgetIncludeBattery;
    [ObservableProperty] private bool _widgetIncludeSpotify = true;
    [ObservableProperty] private string _bridgeActionStatus = "";
    [ObservableProperty] private bool _discordRichPresenceEnabled = true;
    [ObservableProperty] private string _discordApplicationId = "";
    [ObservableProperty] private string _discordRpcStatus = "";
    public Action? RequestDiscordRpcRefresh { get; set; }

    public string LiveStateFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "state.json");

    public string LiveStateIniPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "state.ini");

    [ObservableProperty] private Bitmap? _customBackgroundImage;
    [ObservableProperty] private string? _customBackgroundPath;
    public bool HasCustomBackgroundImage => CustomBackgroundImage is not null;

    [ObservableProperty] private double _backgroundBlur;
    [ObservableProperty] private double _backgroundDim = 0.80;
    [ObservableProperty] private double _backgroundVignette = 0.25;

    [ObservableProperty] private CommunityWallpaperCard? _previewingWallpaper;
    [ObservableProperty] private Bitmap? _previewingFullImage;
    [ObservableProperty] private bool _isStudioOpen;

    private CancellationTokenSource? _saveDebounceCts;

    public bool ShowMainBackground => HasCustomBackgroundImage && !IsStudioOpen;

    public sealed record WallpaperReactionContext(bool IsCleared, string? Title, string? Author, string? Resolution, int PixelWidth, int PixelHeight, bool IsCommunity);
    public WallpaperReactionContext? CurrentWallpaperContext { get; private set; }
    public event Action<WallpaperReactionContext>? WallpaperChanged;

    public string BackgroundBlurText => $"{Math.Round(BackgroundBlur)} px";
    public string BackgroundDimText => $"{Math.Round(BackgroundDim * 100)}%";
    public string BackgroundVignetteText => $"{Math.Round(BackgroundVignette * 100)}%";

    public Bitmap? StudioPreviewImage => PreviewingFullImage ?? PreviewingWallpaper?.Thumbnail ?? CustomBackgroundImage;
    public bool HasStudioPreview => StudioPreviewImage is not null;
    public bool IsPreviewingCommunityWallpaper => PreviewingWallpaper is not null;

    partial void OnBackgroundBlurChanged(double value)
    {
        OnPropertyChanged(nameof(BackgroundBlurText));
        ScheduleSave();
    }

    partial void OnBackgroundDimChanged(double value)
    {
        OnPropertyChanged(nameof(BackgroundDimText));
        ScheduleSave();
    }

    partial void OnBackgroundVignetteChanged(double value)
    {
        OnPropertyChanged(nameof(BackgroundVignetteText));
        ScheduleSave();
    }

    private async void ScheduleSave()
    {
        if (_isLoading) return;
        _saveDebounceCts?.Cancel();
        _saveDebounceCts?.Dispose();
        var cts = new CancellationTokenSource();
        _saveDebounceCts = cts;
        try
        {
            await Task.Delay(250, cts.Token);
            if (!cts.Token.IsCancellationRequested)
            {
                Save();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to save settings", ex);
        }
    }

    partial void OnPreviewingWallpaperChanged(CommunityWallpaperCard? value)
    {
        OnPropertyChanged(nameof(StudioPreviewImage));
        OnPropertyChanged(nameof(HasStudioPreview));
        OnPropertyChanged(nameof(IsPreviewingCommunityWallpaper));
    }

    partial void OnPreviewingFullImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(StudioPreviewImage));
        OnPropertyChanged(nameof(HasStudioPreview));
    }

    partial void OnIsStudioOpenChanged(bool value) => OnPropertyChanged(nameof(ShowMainBackground));

    partial void OnCustomBackgroundImageChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(HasCustomBackgroundImage));
        OnPropertyChanged(nameof(ShowMainBackground));
        OnPropertyChanged(nameof(StudioPreviewImage));
        OnPropertyChanged(nameof(HasStudioPreview));
        OnPropertyChanged(nameof(PublishPreviewImage));
        OnPropertyChanged(nameof(HasPublishPreview));
        OnPropertyChanged(nameof(PublishPreviewResolution));
    }

    [ObservableProperty] private Bitmap? _pendingUploadPreview;
    [ObservableProperty] private string? _pendingUploadPath;

    public Bitmap? PublishPreviewImage => PendingUploadPreview ?? CustomBackgroundImage;
    public bool HasPublishPreview => PublishPreviewImage is not null;
    public string PublishPreviewResolution => PublishPreviewImage is { } bmp
        ? $"{bmp.PixelSize.Width}x{bmp.PixelSize.Height}"
        : "";

    partial void OnPendingUploadPreviewChanged(Bitmap? value)
    {
        OnPropertyChanged(nameof(PublishPreviewImage));
        OnPropertyChanged(nameof(HasPublishPreview));
        OnPropertyChanged(nameof(PublishPreviewResolution));
    }

    public void SetPendingUploadImage(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return;
        try
        {
            using var stream = File.OpenRead(sourcePath);
            var bmp = new Bitmap(stream);
            PendingUploadPreview?.Dispose();
            PendingUploadPath = sourcePath;
            PendingUploadPreview = bmp;
            CommunityStatus = "";
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to load preview image", ex);
        }
    }

    public void ClearPendingUploadImage()
    {
        PendingUploadPreview?.Dispose();
        PendingUploadPreview = null;
        PendingUploadPath = null;
    }

    public ObservableCollection<ThemeItemViewModel> Themes { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> CommunityWallpapers { get; } = new();
    private readonly List<CommunityWallpaperCard> _baseCommunityCards = new();
    private int _communityLoopCursor;

    [ObservableProperty] private string _communityTitle = "";
    [ObservableProperty] private string _communityAuthor = "";
    [ObservableProperty] private string _communityStatus = "";
    [ObservableProperty] private bool _isCommunityBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFilterAll))]
    [NotifyPropertyChangedFor(nameof(IsFilterUltrawide))]
    [NotifyPropertyChangedFor(nameof(IsFilterFavorites))]
    private int _communityFilterMode; // 0 = All, 1 = Ultrawide, 2 = Favorites

    public bool IsFilterAll => CommunityFilterMode == 0;
    public bool IsFilterUltrawide => CommunityFilterMode == 1;
    public bool IsFilterFavorites => CommunityFilterMode == 2;
    private readonly HashSet<string> _favoriteWallpaperIds = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StudioStretch))]
    [NotifyPropertyChangedFor(nameof(BackgroundStretch))]
    [NotifyPropertyChangedFor(nameof(ShowAmbientBackgroundFill))]
    private bool _isStudioFitMode;

    public Avalonia.Media.Stretch StudioStretch => IsStudioFitMode
        ? Avalonia.Media.Stretch.Uniform
        : Avalonia.Media.Stretch.UniformToFill;

    public Avalonia.Media.Stretch BackgroundStretch => StudioStretch;
    public bool ShowAmbientBackgroundFill => ShowMainBackground && IsStudioFitMode;

    internal static bool IsBitmapUltrawide(Bitmap? bmp) =>
        bmp is not null && bmp.PixelSize.Width > 0 && bmp.PixelSize.Height > 0 &&
        (double)bmp.PixelSize.Width / bmp.PixelSize.Height >= 2.05;

    partial void OnCommunityFilterModeChanged(int value) => RebuildFilteredCommunityWallpapers();

    [RelayCommand] private void SetCommunityFilterAll() => CommunityFilterMode = 0;
    [RelayCommand] private void SetCommunityFilterUltrawide() => CommunityFilterMode = 1;
    [RelayCommand] private void SetCommunityFilterFavorites() => CommunityFilterMode = 2;
    [RelayCommand]
    private void ToggleStudioStretch()
    {
        IsStudioFitMode = !IsStudioFitMode;
        if (!IsPreviewingCommunityWallpaper)
            Save();
    }

    private void RebuildFilteredCommunityWallpapers()
    {
        _communityLoopCursor = 0;
        CommunityWallpapers.Clear();
        IEnumerable<CommunityWallpaperCard> source = CommunityFilterMode switch
        {
            1 => _baseCommunityCards.Where(c => c.IsUltrawide),
            2 => _baseCommunityCards.Where(c => c.IsFavorite),
            _ => _baseCommunityCards
        };
        foreach (var card in source)
        {
            if (card.Thumbnail is null)
                _ = card.LoadThumbnailAsync();
            CommunityWallpapers.Add(card);
        }
    }

    [ObservableProperty] private bool _isBugReportOpen;
    [ObservableProperty] private string _bugReportTitle = "";
    [ObservableProperty] private string _bugReportDescription = "";
    [ObservableProperty] private string _bugReportContact = "";
    [ObservableProperty] private string _bugReportStatus = "";
    [ObservableProperty] private bool _isBugReportSending;

    public Func<IEnumerable<string>>? GetAboutMemberNames { get; set; }

    [ObservableProperty] private bool _autoCheckUpdates = true;
    [ObservableProperty] private string _updateStatus = "";
    [ObservableProperty] private string _updateNotes = "";
    [ObservableProperty] private bool _isCheckingUpdates;
    [ObservableProperty] private bool _isUpdateAvailable;
    [ObservableProperty] private bool _isDownloadingUpdate;
    [ObservableProperty] private double _updateProgress;
    private UpdateManifest? _pendingUpdateManifest;
    public string CurrentVersionLabel => L.Format("UpdateCurrentVersion", AppUpdateService.CurrentVersion);

    private static readonly System.Net.Http.HttpClient BugReportHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string BugReportEndpoint = "https://uptime-pc.vercel.app/api/bug-report";

    private readonly ICommunityWallpaperService? _communityWallpapers;
    public ICommunityWallpaperService? WallpaperService => _communityWallpapers;
    private readonly IAccountAuthService? _accountAuth;
    private readonly bool _isLoading;

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsAccountSignedOut))] private bool _isAccountSignedIn;
    public bool IsAccountSignedOut => !IsAccountSignedIn;
    [ObservableProperty] private bool _isMyProfileOpen;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsNotEditingBio))] private bool _isEditingBio;
    public bool IsNotEditingBio => !IsEditingBio;
    [ObservableProperty] private bool _hasAccountBio;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAccountCustomStatus), nameof(AccountStatusBubbleText))] private string _accountCustomStatus = "";
    public bool HasAccountCustomStatus => !string.IsNullOrWhiteSpace(AccountCustomStatus);
    public string AccountStatusBubbleText => HasAccountCustomStatus ? AccountCustomStatus : L["ProfileSetStatus"];
    [ObservableProperty] private bool _isEditingStatus;
    [ObservableProperty] private bool _hasAccountShowcase;
    [ObservableProperty] private bool _hasAccountFavorites;
    [ObservableProperty] private string _showcaseSectionTitle = "SHOWCASE";
    public ObservableCollection<CommunityWallpaperCard> AccountShowcaseWallpapers { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> AccountFavoriteWallpapers { get; } = new();
    private string _accountDiscordId = "";
    public string AccountDiscordId => _accountDiscordId;
    public bool IsOwner => string.Equals(_accountDiscordId, "1233950558184800297", StringComparison.Ordinal)
        || AccountBadges.Any(b => b.Label.Contains("Owner", StringComparison.OrdinalIgnoreCase))
        || string.Equals(AccountUsername, "@ildenteproibito", StringComparison.OrdinalIgnoreCase);
    public string? GetAccountSessionToken() => _accountAuth?.GetSessionToken();
    private Avalonia.Threading.DispatcherTimer? _chatPollTimer;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsProfileTabActive), nameof(IsFriendsTabActive))] private int _profileModalTab;
    public bool IsProfileTabActive => ProfileModalTab == 0;
    public bool IsFriendsTabActive => ProfileModalTab == 1;
    [ObservableProperty] private string _friendSearchQuery = "";
    [ObservableProperty] private string _friendActionStatus = "";
    [ObservableProperty] private bool _hasAccountFriends;
    [ObservableProperty] private bool _hasIncomingRequests;
    [ObservableProperty] private bool _hasOutgoingRequests;
    [ObservableProperty] private bool _hasDiscoverUsers;
    [ObservableProperty] private bool _hasFriendsActivity;
    [ObservableProperty] private string _friendsTabLabel = "Friends";
    public ObservableCollection<FriendCardItem> AccountFriends { get; } = new();
    public ObservableCollection<FriendCardItem> IncomingFriendRequests { get; } = new();
    public ObservableCollection<FriendCardItem> OutgoingFriendRequests { get; } = new();
    public ObservableCollection<FriendCardItem> DiscoverUsers { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> FriendsActivityWallpapers { get; } = new();

    [ObservableProperty] private bool _hasSelectedFriend;
    [ObservableProperty] private bool _isSelectedFriendLoading;
    [ObservableProperty] private bool _selectedFriendNotFound;
    [ObservableProperty] private string _selectedFriendDiscordId = "";
    [ObservableProperty] private string _selectedFriendName = "";
    [ObservableProperty] private string _selectedFriendUsername = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelectedFriendStatus))] private string _selectedFriendStatus = "";
    public bool HasSelectedFriendStatus => !string.IsNullOrWhiteSpace(SelectedFriendStatus);
    [ObservableProperty] private bool _selectedFriendIsFriend;
    [ObservableProperty] private bool _selectedFriendIsPendingOut;
    [ObservableProperty] private bool _selectedFriendIsPendingIn;
    [ObservableProperty] private bool _selectedFriendCanAdd;
    [ObservableProperty] private Bitmap? _selectedFriendAvatar;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasSelectedFriendBanner))] private Bitmap? _selectedFriendBannerBitmap;
    public bool HasSelectedFriendBanner => SelectedFriendBannerBitmap is not null;
    [ObservableProperty] private Avalonia.Media.IBrush _selectedFriendBannerBrush = Avalonia.Media.Brush.Parse("#1F7BC4A8");
    public ObservableCollection<ProfileBadgeCard> SelectedFriendBadges { get; } = new();
    public ObservableCollection<ConnectedAccountCard> SelectedFriendConnections { get; } = new();
    public ObservableCollection<BioLineItem> SelectedFriendBioLines { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> SelectedFriendShowcaseWallpapers { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> SelectedFriendFavoriteWallpapers { get; } = new();
    [ObservableProperty] private bool _hasSelectedFriendShowcase;
    [ObservableProperty] private bool _hasSelectedFriendFavorites;

    public ObservableCollection<FriendChatBubbleItem> SelectedFriendChatMessages { get; } = new();
    [ObservableProperty] private bool _hasChatMessages;
    [ObservableProperty] private string _chatDraftText = "";
    [ObservableProperty] private bool _isChatBusy;
    [ObservableProperty] private bool _isAuthorAlreadyFriend;
    [ObservableProperty] private bool _isAccountBusy;
    [ObservableProperty] private string _accountStatus = "";
    [ObservableProperty] private string _accountDisplayName = "";
    [ObservableProperty] private string _accountUsername = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BioCharCountText))] private string _accountBio = "";
    public string BioCharCountText => $"{(AccountBio ?? "").Length}/500";
    [ObservableProperty] private Bitmap? _accountAvatarBitmap;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAccountBanner))] private Bitmap? _accountBannerBitmap;
    public bool HasAccountBanner => AccountBannerBitmap is not null;
    [ObservableProperty] private Avalonia.Media.IBrush _profileBannerBrush = Avalonia.Media.Brush.Parse("#1F7BC4A8");
    public ObservableCollection<ProfileBadgeCard> AccountBadges { get; } = new();
    public ObservableCollection<ConnectedAccountCard> AccountConnections { get; } = new();
    public ObservableCollection<BioTokenItem> AccountBioLinks { get; } = new();
    public ObservableCollection<BioLineItem> AccountBioLines { get; } = new();
    [ObservableProperty] private bool _hasAccountBioLinks;

    [ObservableProperty] private bool _isAuthorProfileOpen;
    [ObservableProperty] private bool _isAuthorProfileLoading;
    [ObservableProperty] private bool _authorProfileNotFound;
    [ObservableProperty] private string _authorProfileName = "";
    [ObservableProperty] private string _authorProfileUsername = "";
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAuthorProfileStatus))] private string _authorProfileStatus = "";
    public bool HasAuthorProfileStatus => !string.IsNullOrWhiteSpace(AuthorProfileStatus);
    [ObservableProperty] private string _authorProfileBio = "";
    [ObservableProperty] private bool _hasAuthorShowcase;
    [ObservableProperty] private bool _hasAuthorFavorites;
    public ObservableCollection<CommunityWallpaperCard> AuthorShowcaseWallpapers { get; } = new();
    public ObservableCollection<CommunityWallpaperCard> AuthorFavoriteWallpapers { get; } = new();
    [ObservableProperty] private Bitmap? _authorProfileAvatar;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(HasAuthorProfileBanner))] private Bitmap? _authorProfileBannerBitmap;
    public bool HasAuthorProfileBanner => AuthorProfileBannerBitmap is not null;
    [ObservableProperty] private Avalonia.Media.IBrush _authorProfileBannerBrush = Avalonia.Media.Brush.Parse("#1F7BC4A8");
    public ObservableCollection<ProfileBadgeCard> AuthorProfileBadges { get; } = new();
    public ObservableCollection<ConnectedAccountCard> AuthorProfileConnections { get; } = new();
    public ObservableCollection<BioLineItem> AuthorProfileBioLines { get; } = new();

    public SettingsViewModel(ICommunityWallpaperService? communityWallpapers = null, IAccountAuthService? accountAuth = null)
    {
        _communityWallpapers = communityWallpapers;
        _accountAuth = accountAuth;
        _isLoading = true;
        L.PropertyChanged += OnLocalizerChanged;
        try
        {
            var saved = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(FilePath));
            if (saved is not null)
            {
                // 1. Recover custom wallpaper path first, or auto-detect existing wallpaper file
                string? wallpaperCandidate = saved.CustomBackgroundPath;
                var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime");
                if (string.IsNullOrWhiteSpace(wallpaperCandidate) || !File.Exists(wallpaperCandidate))
                {
                    if (Directory.Exists(appDir))
                    {
                        var autoCandidates = Directory.GetFiles(appDir, "custom_wallpaper.*");
                        if (autoCandidates.Length > 0)
                        {
                            wallpaperCandidate = autoCandidates[0];
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(wallpaperCandidate) && File.Exists(wallpaperCandidate))
                {
                    try
                    {
                        CustomBackgroundPath = wallpaperCandidate;
                        CustomBackgroundImage = LoadBackgroundBitmap(wallpaperCandidate);
                        IsStudioFitMode = saved.WallpaperFitMode ?? IsBitmapUltrawide(CustomBackgroundImage);
                    }
                    catch (Exception ex)
                    {
                        AppDiagnostics.Write("Warning", "Could not load saved custom background", ex);
                    }
                }

                if (saved.FavoriteWallpaperIds is { Count: > 0 })
                {
                    foreach (var fid in saved.FavoriteWallpaperIds)
                        if (!string.IsNullOrWhiteSpace(fid)) _favoriteWallpaperIds.Add(fid);
                }

                ShowSession = saved.ShowSession; ShowAdvice = saved.ShowAdvice; ShowSystem = saved.ShowSystem;
                ShowMascot = saved.ShowMascot ?? true;
                ShowGlassContainers = saved.ShowGlassContainers ?? true;
                MascotPosX = saved.MascotPosX;
                MascotPosY = saved.MascotPosY;
                HasCompletedTutorial = saved.HasCompletedTutorial ?? false;
                DiscordRichPresenceEnabled = saved.DiscordRichPresenceEnabled ?? true;
                DiscordApplicationId = saved.DiscordApplicationId ?? "";
                NightReminder = saved.NightReminder; SelectedPreset = saved.Preset;
                LiveStateBridge = saved.LiveStateBridge;
                RestoreWorkspaceOnReboot = saved.RestoreWorkspaceOnReboot;
                PowerPlanAutopilot = saved.PowerPlanAutopilot;
                if (!string.IsNullOrWhiteSpace(saved.KeepAwakeProcessName)) KeepAwakeProcessName = saved.KeepAwakeProcessName;
                KeepAwakeBatteryGuard = saved.KeepAwakeBatteryGuard ?? true;
                AutoCheckUpdates = saved.AutoCheckUpdates ?? true;
                BackgroundBlur = Math.Clamp(saved.BackgroundBlur ?? 0.0, 0.0, 24.0);
                BackgroundDim = Math.Clamp(saved.BackgroundDim ?? 0.80, 0.25, 0.92);
                BackgroundVignette = Math.Clamp(saved.BackgroundVignette ?? 0.25, 0.0, 0.90);
                if (!string.IsNullOrWhiteSpace(saved.Language)) L.Language = saved.Language;
                if (!string.IsNullOrWhiteSpace(saved.ThemeId)) SelectedThemeId = saved.ThemeId;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { }

        // Populate themes and apply selected
        foreach (var def in ThemeManager.Themes)
        {
            Themes.Add(new ThemeItemViewModel(def, def.Id == SelectedThemeId, item => SelectTheme(item.Id)));
        }

        ThemeManager.ApplyTheme(SelectedThemeId);
        ThemeManager.SetGlassContainersEnabled(ShowGlassContainers);
        _isLoading = false;
        Save();
        if (_accountAuth is not null && _accountAuth.HasSavedSession)
            _ = RestoreAccountSessionAsync();
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(IsItalian));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsRussian));
        OnPropertyChanged(nameof(LanguageIndicatorOffset));
        OnPropertyChanged(nameof(CurrentVersionLabel));
        OnPropertyChanged(nameof(AccountStatusBubbleText));
        OnPropertyChanged(nameof(L));
        RefreshShowcaseWallpapers();
        Save();
    }

    partial void OnShowSessionChanged(bool value) => Save();
    partial void OnShowAdviceChanged(bool value) => Save();
    partial void OnShowSystemChanged(bool value) => Save();
    partial void OnShowMascotChanged(bool value) => Save();
    partial void OnShowGlassContainersChanged(bool value)
    {
        ThemeManager.SetGlassContainersEnabled(value);
        Save();
    }

    public bool HasCompletedTutorial { get; private set; }
    public Action? RequestReplayTutorial { get; set; }

    public void MarkTutorialCompleted()
    {
        if (HasCompletedTutorial) return;
        HasCompletedTutorial = true;
        Save();
    }

    [RelayCommand]
    private void ReplayTutorial() => RequestReplayTutorial?.Invoke();

    public void SetMascotPosition(double x, double y)
    {
        MascotPosX = x;
        MascotPosY = y;
        Save();
    }
    partial void OnNightReminderChanged(bool value) => Save();
    partial void OnLiveStateBridgeChanged(bool value) => Save();
    partial void OnRestoreWorkspaceOnRebootChanged(bool value) => Save();
    partial void OnPowerPlanAutopilotChanged(bool value) => Save();
    partial void OnKeepAwakeProcessNameChanged(string value) => Save();
    partial void OnKeepAwakeBatteryGuardChanged(bool value) => Save();
    partial void OnSelectedPresetChanged(string value) => Save();
    partial void OnDiscordRichPresenceEnabledChanged(bool value)
    {
        Save();
        RequestDiscordRpcRefresh?.Invoke();
    }
    partial void OnDiscordApplicationIdChanged(string value)
    {
        Save();
        RequestDiscordRpcRefresh?.Invoke();
    }

    [RelayCommand]
    private void SelectTheme(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == SelectedThemeId && ThemeManager.CurrentTheme.Id == id) return;
        SelectedThemeId = id;
        ThemeManager.ApplyTheme(id);
        foreach (var item in Themes)
        {
            item.IsSelected = item.Id == id;
        }
        Save();
    }

    [RelayCommand]
    private void SetLanguage(string code)
    {
        if (code is "it" or "en" or "ru") L.Language = code;
    }

    [RelayCommand] private void ApplyPreset(string name)
    {
        SelectedPreset = name;
        (ShowSession, ShowAdvice, ShowSystem) = name switch
        {
            "Compact" => (true, false, false),
            "Focus" => (false, true, false),
            _ => (true, true, true),
        };
        Save();
    }

    public void SetCustomBackground(string sourcePath)
    {
        try
        {
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime");
            Directory.CreateDirectory(appDir);
            var ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            var destPath = Path.Combine(appDir, $"custom_wallpaper{ext}");
            if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var oldFile in Directory.GetFiles(appDir, "custom_wallpaper.*"))
                {
                    if (!string.Equals(Path.GetFullPath(oldFile), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(oldFile); } catch { }
                    }
                }
                File.Copy(sourcePath, destPath, true);
            }

            var bmp = LoadBackgroundBitmap(destPath);
            var previous = CustomBackgroundImage;

            CustomBackgroundPath = destPath;
            CustomBackgroundImage = bmp;
            IsStudioFitMode = IsBitmapUltrawide(bmp);
            previous?.Dispose();
            OnPropertyChanged(nameof(HasCustomBackgroundImage));
            Save();

            var ctx = new WallpaperReactionContext(
                false,
                Path.GetFileNameWithoutExtension(sourcePath),
                null,
                null,
                bmp?.PixelSize.Width ?? 0,
                bmp?.PixelSize.Height ?? 0,
                false);
            CurrentWallpaperContext = ctx;
            WallpaperChanged?.Invoke(ctx);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to set custom wallpaper", ex);
        }
    }

    internal static bool TryParseResolution(string? resolution, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(resolution)) return false;
        var parts = resolution.Trim().ToLowerInvariant().Split('x', '×');
        if (parts.Length < 2) return false;
        return int.TryParse(parts[0].Trim(), out width) && int.TryParse(parts[1].Trim(), out height) && width > 0 && height > 0;
    }

    private static int GetTargetDecodeWidth(string? resolutionHint)
    {
        if (TryParseResolution(resolutionHint, out var w, out var h) && (double)w / h >= 2.05)
        {
            // Keep Ultrawide (21:9 / 32:9) wallpapers sharp at native 1440p/1600p vertical detail
            return Math.Clamp(w, 2560, 5120);
        }
        return 2560;
    }

    /// <summary>
    /// Decodes a custom background image capped to 2560px width (or up to 5120px for Ultrawide)
    /// so 4K/8K wallpapers do not force expensive per-frame resampling during UI scrolling.
    /// </summary>
    private static Bitmap LoadBackgroundBitmap(string path, string? resolutionHint = null)
    {
        using var stream = File.OpenRead(path);
        return Bitmap.DecodeToWidth(stream, GetTargetDecodeWidth(resolutionHint), BitmapInterpolationMode.MediumQuality);
    }

    [RelayCommand]
    private void CreateRainmeterSkin()
    {
        try
        {
            var skinDir = RainmeterSkinExporter.CreateSkin(
                LiveStateIniPath,
                WidgetIncludeUptime,
                WidgetIncludeHealth,
                WidgetIncludePower,
                WidgetIncludeBattery,
                WidgetIncludeSpotify);
            BridgeActionStatus = L["BridgeSkinCreated"];
            try
            {
                Process.Start(new ProcessStartInfo { FileName = skinDir, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppDiagnostics.Write("Warning", "Failed to open Rainmeter skin folder", ex);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to create Rainmeter skin", ex);
            BridgeActionStatus = L["BridgeCreateFailed"];
        }
    }

    public void ClearCustomBackground()
    {
        var previous = CustomBackgroundImage;
        CustomBackgroundPath = null;
        CustomBackgroundImage = null;
        previous?.Dispose();
        try
        {
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime");
            if (Directory.Exists(appDir))
            {
                foreach (var oldFile in Directory.GetFiles(appDir, "custom_wallpaper.*"))
                {
                    try { File.Delete(oldFile); } catch { }
                }
            }
        }
        catch { }
        OnPropertyChanged(nameof(HasCustomBackgroundImage));
        Save();
        CurrentWallpaperContext = null;
        WallpaperChanged?.Invoke(new WallpaperReactionContext(true, null, null, null, 0, 0, false));
    }

    [RelayCommand]
    private void OpenCommunityWebsite()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "https://uptime-pc.vercel.app/wallpapers/", UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to open community wallpapers website", ex);
        }
    }

    [RelayCommand]
    private async Task RefreshCommunityWallpapersAsync()
    {
        if (_communityWallpapers is null || !_communityWallpapers.IsConfigured)
        {
            CommunityStatus = L["CommunityNotConfigured"];
            return;
        }
        if (IsCommunityBusy) return;
        IsCommunityBusy = true;
        try
        {
            var items = await _communityWallpapers.GetWallpapersAsync();
            foreach (var old in _baseCommunityCards)
            {
                old.Thumbnail?.Dispose();
                old.Thumbnail = null;
            }
            _baseCommunityCards.Clear();
            foreach (var item in items)
            {
                var card = new CommunityWallpaperCard(item)
                {
                    IsFavorite = _favoriteWallpaperIds.Contains(item.Id)
                };
                _baseCommunityCards.Add(card);
            }
            RebuildFilteredCommunityWallpapers();
            RefreshShowcaseWallpapers();
            RefreshFriendsActivityWallpapers();
            CommunityStatus = items.Count == 0 ? L["CommunityEmpty"] : "";
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to load community wallpapers", ex);
            CommunityStatus = L.Format("CommunityError", ex.Message);
        }
        finally
        {
            IsCommunityBusy = false;
        }
    }

    [RelayCommand]
    private async Task PublishCurrentWallpaperAsync()
    {
        if (_communityWallpapers is null || !_communityWallpapers.IsConfigured)
        {
            CommunityStatus = L["CommunityNotConfigured"];
            return;
        }
        if (IsCommunityBusy) return;

        var uploadPath = !string.IsNullOrWhiteSpace(PendingUploadPath) ? PendingUploadPath : CustomBackgroundPath;
        if (string.IsNullOrWhiteSpace(uploadPath) || !File.Exists(uploadPath))
        {
            CommunityStatus = L["CommunityPickFirst"];
            return;
        }

        IsCommunityBusy = true;
        CommunityStatus = L["CommunityUploading"];
        try
        {
            var res = "1920x1080";
            if (PublishPreviewImage is { } bmp)
                res = $"{bmp.PixelSize.Width}x{bmp.PixelSize.Height}";

            var created = await _communityWallpapers.UploadWallpaperAsync(
                uploadPath,
                CommunityTitle,
                CommunityAuthor,
                res,
                "Custom");
            var card = new CommunityWallpaperCard(created);
            _baseCommunityCards.Insert(0, card);
            if (CommunityFilterMode == 0 || (CommunityFilterMode == 1 && card.IsUltrawide))
            {
                CommunityWallpapers.Insert(0, card);
                _ = card.LoadThumbnailAsync();
            }
            CommunityTitle = "";
            ClearPendingUploadImage();
            CommunityStatus = L["CommunityPublished"];
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to publish community wallpaper", ex);
            CommunityStatus = L.Format("CommunityError", ex.Message);
        }
        finally
        {
            IsCommunityBusy = false;
        }
    }

    public void AppendCommunityLoopBatch(int batchSize = 18)
    {
        var pool = CommunityFilterMode switch
        {
            1 => _baseCommunityCards.Where(c => c.IsUltrawide).ToList(),
            2 => _baseCommunityCards.Where(c => c.IsFavorite).ToList(),
            _ => _baseCommunityCards
        };
        if (pool.Count == 0 || CommunityWallpapers.Count >= 600) return;
        var count = Math.Min(batchSize, pool.Count);
        for (var i = 0; i < count; i++)
        {
            var src = pool[_communityLoopCursor % pool.Count];
            _communityLoopCursor++;
            var clone = new CommunityWallpaperCard(src.Item, src.Thumbnail);
            if (clone.Thumbnail is null)
                _ = clone.LoadThumbnailAsync();
            CommunityWallpapers.Add(clone);
        }
    }

    [RelayCommand]
    private async Task ApplyCommunityWallpaperAsync(CommunityWallpaperCard? card)
    {
        if (card is null || _communityWallpapers is null || IsCommunityBusy) return;
        IsCommunityBusy = true;
        try
        {
            var localPath = await _communityWallpapers.DownloadWallpaperToTempAsync(card.Item, countDownload: false);
            var reusedPreview = PreviewingWallpaper == card ? PreviewingFullImage : null;

            var ext = Path.GetExtension(localPath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
            var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Uptime");
            var destPath = Path.Combine(appDir, $"custom_wallpaper{ext}");

            var bmp = await Task.Run(() =>
            {
                Directory.CreateDirectory(appDir);
                File.Copy(localPath, destPath, true);
                return reusedPreview ?? LoadBackgroundBitmap(destPath, card.Resolution);
            });

            var previous = CustomBackgroundImage;
            CustomBackgroundPath = destPath;
            CustomBackgroundImage = bmp;
            IsStudioFitMode = card.IsUltrawide || IsBitmapUltrawide(bmp);
            if (!ReferenceEquals(previous, bmp))
                previous?.Dispose();
            OnPropertyChanged(nameof(HasCustomBackgroundImage));
            Save();

            var ctx = new WallpaperReactionContext(
                false,
                card.Title,
                card.Author,
                card.Resolution,
                bmp?.PixelSize.Width ?? 0,
                bmp?.PixelSize.Height ?? 0,
                true);
            CurrentWallpaperContext = ctx;
            WallpaperChanged?.Invoke(ctx);

            CommunityStatus = L.Format("CommunityApplied", card.Author);
            IsStudioOpen = false;
            PreviewingWallpaper = null;
            SetPreviewingFullImage(null);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Failed to apply community wallpaper", ex);
            CommunityStatus = L.Format("CommunityError", ex.Message);
        }
        finally
        {
            IsCommunityBusy = false;
        }
    }

    private void SetPreviewingFullImage(Bitmap? next)
    {
        var prev = PreviewingFullImage;
        if (ReferenceEquals(prev, next)) return;
        PreviewingFullImage = next;
        prev?.Dispose();
    }

    [RelayCommand]
    private async Task PreviewCommunityWallpaperAsync(CommunityWallpaperCard? card)
    {
        if (card is null) return;
        SetPreviewingFullImage(null);
        PreviewingWallpaper = card;
        IsStudioFitMode = card.IsUltrawide;
        IsStudioOpen = true;

        if (_communityWallpapers is null) return;
        try
        {
            var localPath = await _communityWallpapers.DownloadWallpaperToTempAsync(card.Item, countDownload: false);
            if (PreviewingWallpaper == card && IsStudioOpen && File.Exists(localPath))
            {
                var fullBmp = await Task.Run(() => LoadBackgroundBitmap(localPath, card.Resolution));
                if (PreviewingWallpaper == card && IsStudioOpen)
                    SetPreviewingFullImage(fullBmp);
                else
                    fullBmp.Dispose();
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Could not load full-res wallpaper for studio preview", ex);
        }
    }

    [RelayCommand]
    private void OpenCurrentWallpaperStudio()
    {
        if (!HasCustomBackgroundImage) return;
        SetPreviewingFullImage(null);
        PreviewingWallpaper = null;
        IsStudioOpen = true;
    }

    [RelayCommand]
    private void CloseWallpaperPreview()
    {
        IsStudioOpen = false;
        PreviewingWallpaper = null;
        SetPreviewingFullImage(null);
    }

    [RelayCommand]
    private void ResetBackgroundEffects()
    {
        BackgroundBlur = 0.0;
        BackgroundDim = 0.80;
        BackgroundVignette = 0.25;
    }

    private void Save()
    {
        if (_isLoading) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(new SettingsData(
                ShowSession, ShowAdvice, ShowSystem, NightReminder, SelectedPreset, L.Language, SelectedThemeId, CustomBackgroundPath,
                LiveStateBridge, RestoreWorkspaceOnReboot, PowerPlanAutopilot, KeepAwakeProcessName, KeepAwakeBatteryGuard, AutoCheckUpdates,
                BackgroundBlur, BackgroundDim, BackgroundVignette, ShowMascot, MascotPosX, MascotPosY, HasCompletedTutorial,
                DiscordRichPresenceEnabled, DiscordApplicationId, ShowGlassContainers, IsStudioFitMode, _favoriteWallpaperIds.ToList())));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
    private async Task RestoreAccountSessionAsync()
    {
        if (_accountAuth is null) return;
        try
        {
            var profile = await _accountAuth.RestoreSessionAsync();
            if (profile is not null)
                await ApplyLoadedProfileAsync(profile);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to restore Discord account session", ex);
        }
    }

    [RelayCommand]
    private async Task LoginWithDiscordAsync()
    {
        if (_accountAuth is null || IsAccountBusy) return;
        IsAccountBusy = true;
        AccountStatus = "Opening Discord OAuth2 (PKCE) in your browser...";
        try
        {
            var profile = await _accountAuth.LoginWithDiscordAsync();
            await ApplyLoadedProfileAsync(profile);
            AccountStatus = "Signed in securely with Discord.";
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Discord OAuth login failed", ex);
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private void OpenMyProfile()
    {
        RefreshShowcaseWallpapers();
        IsMyProfileOpen = true;
    }

    [RelayCommand]
    private void CloseMyProfile()
    {
        IsMyProfileOpen = false;
        IsEditingBio = false;
        IsEditingStatus = false;
    }

    [RelayCommand]
    private void StartEditStatus()
    {
        IsEditingStatus = true;
    }

    [RelayCommand]
    private void CancelEditStatus()
    {
        IsEditingStatus = false;
    }

    [RelayCommand]
    private async Task SaveCustomStatusAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        try
        {
            var profile = await _accountAuth.UpdateCustomStatusAsync(AccountCustomStatus);
            await ApplyLoadedProfileAsync(profile);
            IsEditingStatus = false;
            AccountStatus = "Status updated.";
        }
        catch (Exception ex)
        {
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task ClearCustomStatusAsync()
    {
        AccountCustomStatus = "";
        await SaveCustomStatusAsync();
    }

    [RelayCommand]
    private void SetProfileTab() => ProfileModalTab = 0;

    [RelayCommand]
    private void SetFriendsTab() => ProfileModalTab = 1;

    [RelayCommand]
    private async Task AddFriendAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        var q = (FriendSearchQuery ?? "").Trim();
        if (string.IsNullOrWhiteSpace(q))
        {
            FriendActionStatus = "Enter a Discord username or display name.";
            return;
        }
        IsAccountBusy = true;
        FriendActionStatus = "";
        try
        {
            var prevCount = AccountFriends.Count;
            var profile = await _accountAuth.AddFriendAsync(q);
            await ApplyLoadedProfileAsync(profile);
            FriendSearchQuery = "";
            FriendActionStatus = profile.Friends.Count > prevCount
                ? "Friend added."
                : "Friend request sent.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendFriendRequestToUserAsync(FriendCardItem? user)
    {
        if (_accountAuth is null || user is null || !IsAccountSignedIn || IsAccountBusy) return;
        var query = string.IsNullOrWhiteSpace(user.Username) ? user.DisplayName : user.Username;
        if (string.IsNullOrWhiteSpace(query)) return;
        IsAccountBusy = true;
        FriendActionStatus = "";
        try
        {
            var prevCount = AccountFriends.Count;
            var profile = await _accountAuth.AddFriendAsync(query);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = profile.Friends.Count > prevCount
                ? $"Added {user.DisplayName}!"
                : $"Request sent to {user.DisplayName}.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task AcceptFriendRequestAsync(FriendCardItem? friend)
    {
        if (_accountAuth is null || friend is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        FriendActionStatus = "";
        try
        {
            var profile = await _accountAuth.AcceptFriendAsync(friend.DiscordId);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = $"You and {friend.DisplayName} are now friends!";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeclineFriendRequestAsync(FriendCardItem? friend)
    {
        if (_accountAuth is null || friend is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        FriendActionStatus = "";
        try
        {
            var profile = await _accountAuth.DeclineFriendAsync(friend.DiscordId);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = "Request declined.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveFriendAsync(FriendCardItem? friend)
    {
        if (_accountAuth is null || friend is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        FriendActionStatus = "";
        try
        {
            var profile = await _accountAuth.RemoveFriendAsync(friend.DiscordId);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = "Friend removed.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectFriendAsync(FriendCardItem? friend)
    {
        if (friend is null || _accountAuth is null) return;
        HasSelectedFriend = true;
        IsSelectedFriendLoading = true;
        SelectedFriendNotFound = false;
        SelectedFriendDiscordId = friend.DiscordId;
        SelectedFriendName = friend.DisplayName;
        SelectedFriendUsername = friend.UsernameLabel;
        SelectedFriendStatus = friend.CustomStatus;
        SelectedFriendAvatar?.Dispose();
        SelectedFriendAvatar = null;
        SelectedFriendBannerBitmap?.Dispose();
        SelectedFriendBannerBitmap = null;
        SelectedFriendBannerBrush = Avalonia.Media.Brush.Parse("#1F7BC4A8");
        SelectedFriendBadges.Clear();
        SelectedFriendConnections.Clear();
        SelectedFriendBioLines.Clear();
        SelectedFriendShowcaseWallpapers.Clear();
        SelectedFriendFavoriteWallpapers.Clear();
        HasSelectedFriendShowcase = false;
        HasSelectedFriendFavorites = false;
        OnPropertyChanged(nameof(HasSelectedFriendConnections));
        OnPropertyChanged(nameof(HasSelectedFriendSyncWallpaper));
        SelectedFriendChatMessages.Clear();
        HasChatMessages = false;
        ChatDraftText = "";
        UpdateSelectedFriendRelationshipFlags();
        if (SelectedFriendIsFriend)
        {
            EnsureChatPollingStarted();
            _ = LoadFriendChatAsync();
        }

        try
        {
            var query = string.IsNullOrWhiteSpace(friend.Username) ? friend.DisplayName : friend.Username;
            var profile = await _accountAuth.GetPublicProfileAsync(query);
            if (profile is null)
            {
                SelectedFriendNotFound = true;
                return;
            }

            SelectedFriendDiscordId = profile.DiscordId;
            SelectedFriendName = string.IsNullOrWhiteSpace(profile.DisplayName) ? profile.Username : profile.DisplayName;
            SelectedFriendUsername = string.IsNullOrWhiteSpace(profile.Username) ? "" : $"@{profile.Username}";
            SelectedFriendStatus = profile.CustomStatus ?? "";
            var bioText = string.IsNullOrWhiteSpace(profile.Bio) ? "No bio set yet." : profile.Bio;
            foreach (var line in ParseBioLines(bioText))
                SelectedFriendBioLines.Add(line);
            SelectedFriendBannerBrush = ResolveBannerBrush(profile.BannerColor, profile.Badges);
            foreach (var b in profile.Badges) SelectedFriendBadges.Add(new ProfileBadgeCard(b));
            foreach (var c in profile.Connections.Where(x => !x.Hidden))
                SelectedFriendConnections.Add(new ConnectedAccountCard(c));

            var matches = _baseCommunityCards
                .Where(c => string.Equals(c.Item.Author?.Trim(), profile.DisplayName?.Trim(), StringComparison.OrdinalIgnoreCase)
                         || (!string.IsNullOrWhiteSpace(profile.Username) && string.Equals(c.Item.Author?.Trim(), profile.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Take(4)
                .ToList();
            foreach (var w in matches)
            {
                SelectedFriendShowcaseWallpapers.Add(w);
                _ = w.LoadThumbnailAsync();
            }
            HasSelectedFriendShowcase = SelectedFriendShowcaseWallpapers.Count > 0;

            var favSet = new HashSet<string>(profile.FavoriteWallpaperIds, StringComparer.OrdinalIgnoreCase);
            foreach (var w in _baseCommunityCards.Where(c => favSet.Contains(c.Item.Id)).Take(4))
            {
                SelectedFriendFavoriteWallpapers.Add(w);
                _ = w.LoadThumbnailAsync();
            }
            HasSelectedFriendFavorites = SelectedFriendFavoriteWallpapers.Count > 0;
            OnPropertyChanged(nameof(HasSelectedFriendConnections));
            OnPropertyChanged(nameof(HasSelectedFriendSyncWallpaper));

            UpdateSelectedFriendRelationshipFlags();
            if (SelectedFriendIsFriend)
            {
                EnsureChatPollingStarted();
                _ = LoadFriendChatAsync();
            }

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
            {
                try
                {
                    var bytes = await http.GetByteArrayAsync(profile.AvatarUrl);
                    using var ms = new MemoryStream(bytes);
                    var bmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 128));
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => SelectedFriendAvatar = bmp);
                }
                catch { /* ignore */ }
            }
            if (!string.IsNullOrWhiteSpace(profile.BannerUrl))
            {
                try
                {
                    var bBytes = await http.GetByteArrayAsync(profile.BannerUrl);
                    using var bms = new MemoryStream(bBytes);
                    var bBmp = await Task.Run(() => Bitmap.DecodeToWidth(bms, 700));
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => SelectedFriendBannerBitmap = bBmp);
                }
                catch { /* ignore */ }
            }
        }
        catch
        {
            SelectedFriendNotFound = true;
        }
        finally
        {
            IsSelectedFriendLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddSelectedFriendAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        var target = SelectedFriendUsername.TrimStart('@').Trim();
        if (string.IsNullOrWhiteSpace(target)) target = SelectedFriendName.Trim();
        if (string.IsNullOrWhiteSpace(target)) return;
        IsAccountBusy = true;
        try
        {
            var prevCount = AccountFriends.Count;
            var profile = await _accountAuth.AddFriendAsync(target);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = profile.Friends.Count > prevCount
                ? "Friend added."
                : "Friend request sent.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveSelectedFriendAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy || string.IsNullOrWhiteSpace(SelectedFriendDiscordId)) return;
        IsAccountBusy = true;
        try
        {
            var profile = await _accountAuth.RemoveFriendAsync(SelectedFriendDiscordId);
            await ApplyLoadedProfileAsync(profile);
            FriendActionStatus = "Friend removed.";
            UpdateSelectedFriendRelationshipFlags();
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    private void UpdateSelectedFriendRelationshipFlags()
    {
        var id = SelectedFriendDiscordId;
        if (string.IsNullOrWhiteSpace(id))
        {
            SelectedFriendIsFriend = false;
            SelectedFriendIsPendingOut = false;
            SelectedFriendIsPendingIn = false;
            SelectedFriendCanAdd = false;
            return;
        }
        SelectedFriendIsFriend = AccountFriends.Any(f => string.Equals(f.DiscordId, id, StringComparison.Ordinal));
        SelectedFriendIsPendingOut = OutgoingFriendRequests.Any(f => string.Equals(f.DiscordId, id, StringComparison.Ordinal));
        SelectedFriendIsPendingIn = IncomingFriendRequests.Any(f => string.Equals(f.DiscordId, id, StringComparison.Ordinal));
        SelectedFriendCanAdd = !SelectedFriendIsFriend && !SelectedFriendIsPendingOut && !SelectedFriendIsPendingIn
                               && !string.Equals(id, _accountDiscordId, StringComparison.Ordinal);
    }

    private void EnsureChatPollingStarted()
    {
        if (_chatPollTimer is not null) return;
        _chatPollTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _chatPollTimer.Tick += async (_, _) =>
        {
            if (IsAccountSignedIn && HasSelectedFriend && SelectedFriendIsFriend && !IsChatBusy)
                await LoadFriendChatAsync();
        };
        _chatPollTimer.Start();
    }

    [RelayCommand]
    private async Task LoadFriendChatAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || !SelectedFriendIsFriend || string.IsNullOrWhiteSpace(SelectedFriendDiscordId))
            return;
        var targetId = SelectedFriendDiscordId;
        try
        {
            var msgs = await _accountAuth.GetFriendChatAsync(targetId);
            if (!string.Equals(SelectedFriendDiscordId, targetId, StringComparison.Ordinal)) return;
            ApplyChatMessages(msgs);
        }
        catch { /* ignore transient poll errors */ }
    }

    [RelayCommand]
    private async Task SendChatMessageAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || !SelectedFriendIsFriend || IsChatBusy) return;
        var text = (ChatDraftText ?? "").Trim();
        var targetId = SelectedFriendDiscordId;
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(targetId)) return;

        IsChatBusy = true;
        try
        {
            var msgs = await _accountAuth.SendFriendMessageAsync(targetId, text);
            ChatDraftText = "";
            if (string.Equals(SelectedFriendDiscordId, targetId, StringComparison.Ordinal))
                ApplyChatMessages(msgs);
        }
        catch (Exception ex)
        {
            FriendActionStatus = ex.Message;
        }
        finally
        {
            IsChatBusy = false;
        }
    }

    private void ApplyChatMessages(IReadOnlyList<FriendChatMessage> msgs)
    {
        SelectedFriendChatMessages.Clear();
        foreach (var m in msgs)
        {
            var isMine = string.Equals(m.FromDiscordId, _accountDiscordId, StringComparison.Ordinal);
            SelectedFriendChatMessages.Add(new FriendChatBubbleItem(m, isMine));
        }
        HasChatMessages = SelectedFriendChatMessages.Count > 0;
    }

    public bool HasSelectedFriendConnections => SelectedFriendConnections.Count > 0;
    public bool HasSelectedFriendSyncWallpaper => SelectedFriendShowcaseWallpapers.Count > 0 || SelectedFriendFavoriteWallpapers.Count > 0;

    [RelayCommand]
    private async Task SyncFriendWallpaperAsync()
    {
        var targetCard = SelectedFriendShowcaseWallpapers.FirstOrDefault() ?? SelectedFriendFavoriteWallpapers.FirstOrDefault();
        if (targetCard != null)
        {
            await ApplyCommunityWallpaperAsync(targetCard);
            FriendActionStatus = L["FriendsWallpaperSynced"];
        }
    }

    [RelayCommand]
    private async Task OpenFriendProfileAsync(FriendCardItem? friend)
    {
        if (friend is null) return;
        IsMyProfileOpen = false;
        await OpenAuthorProfileByNameAsync(string.IsNullOrWhiteSpace(friend.Username) ? friend.DisplayName : friend.Username);
    }

    [RelayCommand]
    private async Task AddAuthorAsFriendAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        var target = AuthorProfileUsername.TrimStart('@').Trim();
        if (string.IsNullOrWhiteSpace(target)) target = AuthorProfileName.Trim();
        if (string.IsNullOrWhiteSpace(target)) return;
        IsAccountBusy = true;
        try
        {
            var profile = await _accountAuth.AddFriendAsync(target);
            await ApplyLoadedProfileAsync(profile);
            IsAuthorAlreadyFriend = true;
        }
        catch { /* ignore */ }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private void StartEditBio()
    {
        IsEditingBio = true;
    }

    [RelayCommand]
    private void CancelEditBio()
    {
        IsEditingBio = false;
    }

    [RelayCommand]
    private async Task SaveAccountBioAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        try
        {
            var hidden = AccountConnections.Where(c => c.IsHidden).Select(c => c.Key).ToList();
            var profile = await _accountAuth.UpdateProfileAsync(AccountBio, hidden);
            await ApplyLoadedProfileAsync(profile);
            IsEditingBio = false;
            AccountStatus = "Profile bio saved.";
        }
        catch (Exception ex)
        {
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleConnectionVisibilityAsync(ConnectedAccountCard? item)
    {
        if (_accountAuth is null || item is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        try
        {
            item.IsHidden = !item.IsHidden;
            var hidden = AccountConnections.Where(c => c.IsHidden).Select(c => c.Key).ToList();
            var profile = await _accountAuth.UpdateProfileAsync(null, hidden);
            await ApplyLoadedProfileAsync(profile);
            AccountStatus = item.IsHidden ? $"{item.Platform} hidden." : $"{item.Platform} visible.";
        }
        catch (Exception ex)
        {
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    partial void OnAccountBioChanged(string value)
    {
        AccountBioLinks.Clear();
        foreach (var link in ExtractBioLinks(value))
            AccountBioLinks.Add(link);
        HasAccountBioLinks = AccountBioLinks.Count > 0;
        AccountBioLines.Clear();
        HasAccountBio = !string.IsNullOrWhiteSpace(value);
        if (HasAccountBio)
        {
            foreach (var line in ParseBioLines(value))
                AccountBioLines.Add(line);
        }
    }

    [RelayCommand]
    private void OpenExternalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return;
        if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase))
            return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri.AbsoluteUri, UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    private void OpenConnectionUrl(ConnectedAccountCard? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Url)) return;
        OpenExternalUrl(item.Url);
    }

    [RelayCommand]
    private async Task RefreshAccountAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        try
        {
            var profile = await _accountAuth.RestoreSessionAsync();
            if (profile is not null)
            {
                await ApplyLoadedProfileAsync(profile);
                AccountStatus = "Profile refreshed.";
            }
        }
        catch (Exception ex)
        {
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteWallpaperAsync(CommunityWallpaperCard? card)
    {
        if (card is null) return;
        card.IsFavorite = !card.IsFavorite;
        if (card.IsFavorite) _favoriteWallpaperIds.Add(card.Item.Id);
        else _favoriteWallpaperIds.Remove(card.Item.Id);

        foreach (var c in _baseCommunityCards)
        {
            if (string.Equals(c.Item.Id, card.Item.Id, StringComparison.OrdinalIgnoreCase))
                c.IsFavorite = card.IsFavorite;
        }
        foreach (var c in CommunityWallpapers)
        {
            if (string.Equals(c.Item.Id, card.Item.Id, StringComparison.OrdinalIgnoreCase))
                c.IsFavorite = card.IsFavorite;
        }

        if (CommunityFilterMode == 2 && !card.IsFavorite)
            RebuildFilteredCommunityWallpapers();

        Save();
        RefreshShowcaseWallpapers();

        if (IsAccountSignedIn && _accountAuth is not null)
        {
            try
            {
                await _accountAuth.UpdateFavoritesAsync(_favoriteWallpaperIds.ToList());
            }
            catch { /* keep local favorite on transient network error */ }
        }
    }

    [RelayCommand]
    private async Task OpenAuthorProfileAsync(CommunityWallpaperCard? card)
    {
        if (card is null) return;
        await OpenAuthorProfileByNameAsync(card.Author);
    }

    private async Task OpenAuthorProfileByNameAsync(string authorName)
    {
        if (string.IsNullOrWhiteSpace(authorName) || _accountAuth is null) return;
        IsAuthorProfileOpen = true;
        IsAuthorProfileLoading = true;
        AuthorProfileNotFound = false;
        IsAuthorAlreadyFriend = false;
        AuthorProfileName = authorName;
        AuthorProfileUsername = "";
        AuthorProfileStatus = "";
        AuthorProfileBio = "";
        AuthorProfileAvatar?.Dispose();
        AuthorProfileAvatar = null;
        AuthorProfileBannerBitmap?.Dispose();
        AuthorProfileBannerBitmap = null;
        AuthorProfileBannerBrush = Avalonia.Media.Brush.Parse("#1F7BC4A8");
        AuthorProfileBadges.Clear();
        AuthorProfileConnections.Clear();
        AuthorProfileBioLines.Clear();
        AuthorShowcaseWallpapers.Clear();
        AuthorFavoriteWallpapers.Clear();
        HasAuthorShowcase = false;
        HasAuthorFavorites = false;

        try
        {
            var profile = await _accountAuth.GetPublicProfileAsync(authorName);
            if (profile is null)
            {
                AuthorProfileNotFound = true;
                return;
            }

            AuthorProfileName = profile.DisplayName;
            AuthorProfileUsername = string.IsNullOrWhiteSpace(profile.Username) ? "" : $"@{profile.Username}";
            IsAuthorAlreadyFriend = AccountFriends.Any(f => string.Equals(f.DiscordId, profile.DiscordId, StringComparison.Ordinal));
            AuthorProfileStatus = profile.CustomStatus ?? "";
            AuthorProfileBio = string.IsNullOrWhiteSpace(profile.Bio) ? "No bio set yet." : profile.Bio;
            foreach (var line in ParseBioLines(AuthorProfileBio))
                AuthorProfileBioLines.Add(line);
            AuthorProfileBannerBrush = ResolveBannerBrush(profile.BannerColor, profile.Badges);
            foreach (var b in profile.Badges) AuthorProfileBadges.Add(new ProfileBadgeCard(b));
            foreach (var c in profile.Connections.Where(x => !x.Hidden))
                AuthorProfileConnections.Add(new ConnectedAccountCard(c));

            var authorMatches = _baseCommunityCards
                .Where(c => string.Equals(c.Item.Author?.Trim(), profile.DisplayName?.Trim(), StringComparison.OrdinalIgnoreCase)
                         || (!string.IsNullOrWhiteSpace(profile.Username) && string.Equals(c.Item.Author?.Trim(), profile.Username.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Take(3)
                .ToList();
            foreach (var w in authorMatches)
            {
                AuthorShowcaseWallpapers.Add(w);
                _ = w.LoadThumbnailAsync();
            }
            HasAuthorShowcase = AuthorShowcaseWallpapers.Count > 0;

            var authorFavSet = new HashSet<string>(profile.FavoriteWallpaperIds, StringComparer.OrdinalIgnoreCase);
            foreach (var w in _baseCommunityCards.Where(c => authorFavSet.Contains(c.Item.Id)).Take(4))
            {
                AuthorFavoriteWallpapers.Add(w);
                _ = w.LoadThumbnailAsync();
            }
            HasAuthorFavorites = AuthorFavoriteWallpapers.Count > 0;

            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
            {
                var bytes = await http.GetByteArrayAsync(profile.AvatarUrl);
                using var ms = new MemoryStream(bytes);
                var avBmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 160));
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => AuthorProfileAvatar = avBmp);
            }
            if (!string.IsNullOrWhiteSpace(profile.BannerUrl))
            {
                try
                {
                    var bBytes = await http.GetByteArrayAsync(profile.BannerUrl);
                    using var bms = new MemoryStream(bBytes);
                    var bnBmp = await Task.Run(() => Bitmap.DecodeToWidth(bms, 600));
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => AuthorProfileBannerBitmap = bnBmp);
                }
                catch { /* ignore banner load error */ }
            }
        }
        catch
        {
            AuthorProfileNotFound = true;
        }
        finally
        {
            IsAuthorProfileLoading = false;
        }
    }

    [RelayCommand]
    private void CloseAuthorProfile()
    {
        IsAuthorProfileOpen = false;
    }

    [RelayCommand]
    private void LogoutAccount()
    {
        _accountAuth?.Logout();
        ClearLoadedProfile();
        AccountStatus = "Signed out.";
    }

    [RelayCommand]
    private async Task DeleteAccountAsync()
    {
        if (_accountAuth is null || !IsAccountSignedIn || IsAccountBusy) return;
        IsAccountBusy = true;
        try
        {
            await _accountAuth.DeleteAccountAsync();
            ClearLoadedProfile();
            AccountStatus = "Account and cloud profile deleted.";
        }
        catch (Exception ex)
        {
            AccountStatus = ex.Message;
        }
        finally
        {
            IsAccountBusy = false;
        }
    }

    public void Dispose()
    {
        _chatPollTimer?.Stop();
        _chatPollTimer = null;
        _saveDebounceCts?.Cancel();
        _saveDebounceCts?.Dispose();
        L.PropertyChanged -= OnLocalizerChanged;
        SetPreviewingFullImage(null);
        ClearLoadedProfile();
    }

    private void ClearLoadedProfile()
    {
        IsAccountSignedIn = false;
        IsEditingBio = false;
        IsEditingStatus = false;
        AccountDisplayName = "";
        AccountUsername = "";
        AccountCustomStatus = "";
        AccountBio = "";
        HasAccountBio = false;
        AccountAvatarBitmap?.Dispose();
        AccountAvatarBitmap = null;
        AccountBannerBitmap?.Dispose();
        AccountBannerBitmap = null;
        AccountBadges.Clear();
        AccountConnections.Clear();
        AccountBioLinks.Clear();
        AccountBioLines.Clear();
        AccountShowcaseWallpapers.Clear();
        AccountFavoriteWallpapers.Clear();
        HasAccountShowcase = false;
        HasAccountFavorites = false;
        HasAccountBioLinks = false;
        _accountDiscordId = "";
        OnPropertyChanged(nameof(AccountDiscordId));
        OnPropertyChanged(nameof(IsOwner));
        _chatPollTimer?.Stop();
        _chatPollTimer = null;
        foreach (var fr in AccountFriends) fr.Dispose();
        AccountFriends.Clear();
        foreach (var fr in IncomingFriendRequests) fr.Dispose();
        IncomingFriendRequests.Clear();
        foreach (var fr in OutgoingFriendRequests) fr.Dispose();
        OutgoingFriendRequests.Clear();
        foreach (var fr in DiscoverUsers) fr.Dispose();
        DiscoverUsers.Clear();
        FriendsActivityWallpapers.Clear();
        HasAccountFriends = false;
        HasIncomingRequests = false;
        HasOutgoingRequests = false;
        HasDiscoverUsers = false;
        HasFriendsActivity = false;
        HasSelectedFriend = false;
        SelectedFriendDiscordId = "";
        SelectedFriendAvatar?.Dispose();
        SelectedFriendAvatar = null;
        SelectedFriendBannerBitmap?.Dispose();
        SelectedFriendBannerBitmap = null;
        SelectedFriendBadges.Clear();
        SelectedFriendConnections.Clear();
        SelectedFriendBioLines.Clear();
        SelectedFriendShowcaseWallpapers.Clear();
        SelectedFriendFavoriteWallpapers.Clear();
        HasSelectedFriendShowcase = false;
        HasSelectedFriendFavorites = false;
        OnPropertyChanged(nameof(HasSelectedFriendConnections));
        OnPropertyChanged(nameof(HasSelectedFriendSyncWallpaper));
        SelectedFriendChatMessages.Clear();
        HasChatMessages = false;
        ChatDraftText = "";
        FriendsTabLabel = "Friends";
        FriendSearchQuery = "";
        FriendActionStatus = "";
    }

    private static void PopulateFriendCards(ObservableCollection<FriendCardItem> target, IEnumerable<FriendProfileSummary> items)
    {
        foreach (var fr in target) fr.Dispose();
        target.Clear();
        foreach (var f in items)
        {
            var card = new FriendCardItem(f);
            target.Add(card);
            _ = card.LoadAvatarAsync();
        }
    }

    private void RefreshFriendsActivityWallpapers()
    {
        FriendsActivityWallpapers.Clear();
        if (AccountFriends.Count == 0)
        {
            HasFriendsActivity = false;
            return;
        }

        var friendAuthors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fr in AccountFriends)
        {
            if (!string.IsNullOrWhiteSpace(fr.DisplayName)) friendAuthors.Add(fr.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(fr.Username)) friendAuthors.Add(fr.Username.Trim().TrimStart('@'));
        }

        foreach (var card in _baseCommunityCards)
        {
            var author = (card.Item.Author ?? "").Trim().TrimStart('@');
            if (string.IsNullOrWhiteSpace(author) || !friendAuthors.Contains(author)) continue;
            FriendsActivityWallpapers.Add(card);
            _ = card.LoadThumbnailAsync();
            if (FriendsActivityWallpapers.Count >= 6) break;
        }

        HasFriendsActivity = FriendsActivityWallpapers.Count > 0;
    }

    private void RefreshShowcaseWallpapers()
    {
        AccountShowcaseWallpapers.Clear();
        AccountFavoriteWallpapers.Clear();
        var displayName = (AccountDisplayName ?? "").Trim();
        var userHandle = (AccountUsername ?? "").Trim().TrimStart('@');
        var published = _baseCommunityCards
            .Where(c => (!string.IsNullOrWhiteSpace(displayName) && string.Equals(c.Item.Author?.Trim(), displayName, StringComparison.OrdinalIgnoreCase))
                     || (!string.IsNullOrWhiteSpace(userHandle) && string.Equals(c.Item.Author?.Trim(), userHandle, StringComparison.OrdinalIgnoreCase)))
            .Take(3)
            .ToList();
        ShowcaseSectionTitle = L["ProfilePublishedWallpapers"];
        foreach (var w in published)
        {
            AccountShowcaseWallpapers.Add(w);
            _ = w.LoadThumbnailAsync();
        }
        HasAccountShowcase = AccountShowcaseWallpapers.Count > 0;

        var favs = _baseCommunityCards.Where(c => _favoriteWallpaperIds.Contains(c.Item.Id)).Take(6).ToList();
        foreach (var w in favs)
        {
            AccountFavoriteWallpapers.Add(w);
            _ = w.LoadThumbnailAsync();
        }
        HasAccountFavorites = AccountFavoriteWallpapers.Count > 0;
    }

    private async Task ApplyLoadedProfileAsync(UserProfileData profile)
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsAccountSignedIn = true;
            _accountDiscordId = profile.DiscordId;
            OnPropertyChanged(nameof(AccountDiscordId));
            OnPropertyChanged(nameof(IsOwner));
            AccountDisplayName = profile.DisplayName;
            AccountUsername = string.IsNullOrWhiteSpace(profile.Username) ? "" : $"@{profile.Username}";
            AccountCustomStatus = profile.CustomStatus ?? "";
            AccountBio = profile.Bio ?? "";
            HasAccountBio = !string.IsNullOrWhiteSpace(AccountBio);
            if (string.IsNullOrWhiteSpace(CommunityAuthor))
                CommunityAuthor = profile.DisplayName;

            AccountBadges.Clear();
            foreach (var b in profile.Badges) AccountBadges.Add(new ProfileBadgeCard(b));

            ProfileBannerBrush = ResolveBannerBrush(profile.BannerColor, profile.Badges);

            foreach (var favId in profile.FavoriteWallpaperIds)
                _favoriteWallpaperIds.Add(favId);
            foreach (var c in _baseCommunityCards)
                c.IsFavorite = _favoriteWallpaperIds.Contains(c.Item.Id);
            foreach (var c in CommunityWallpapers)
                c.IsFavorite = _favoriteWallpaperIds.Contains(c.Item.Id);

            AccountConnections.Clear();
            foreach (var c in profile.Connections)
                AccountConnections.Add(new ConnectedAccountCard(c));

            PopulateFriendCards(AccountFriends, profile.Friends);
            PopulateFriendCards(IncomingFriendRequests, profile.IncomingRequests);
            PopulateFriendCards(OutgoingFriendRequests, profile.OutgoingRequests);
            PopulateFriendCards(DiscoverUsers, profile.DiscoverUsers);

            HasAccountFriends = AccountFriends.Count > 0;
            HasIncomingRequests = IncomingFriendRequests.Count > 0;
            HasOutgoingRequests = OutgoingFriendRequests.Count > 0;
            HasDiscoverUsers = DiscoverUsers.Count > 0;
            FriendsTabLabel = AccountFriends.Count > 0 ? $"Friends ({AccountFriends.Count})" : "Friends";

            RefreshFriendsActivityWallpapers();
            UpdateSelectedFriendRelationshipFlags();

            if (!HasSelectedFriend && AccountFriends.Count > 0)
                _ = SelectFriendAsync(AccountFriends[0]);

            RefreshShowcaseWallpapers();
        });

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        if (!string.IsNullOrWhiteSpace(profile.AvatarUrl))
        {
            try
            {
                var bytes = await http.GetByteArrayAsync(profile.AvatarUrl);
                using var ms = new MemoryStream(bytes);
                var bmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 160));
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var prev = AccountAvatarBitmap;
                    AccountAvatarBitmap = bmp;
                    prev?.Dispose();
                });
            }
            catch { /* ignore avatar load error */ }
        }

        if (!string.IsNullOrWhiteSpace(profile.BannerUrl))
        {
            try
            {
                var bBytes = await http.GetByteArrayAsync(profile.BannerUrl);
                using var bms = new MemoryStream(bBytes);
                var bBmp = await Task.Run(() => Bitmap.DecodeToWidth(bms, 600));
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var prev = AccountBannerBitmap;
                    AccountBannerBitmap = bBmp;
                    prev?.Dispose();
                });
            }
            catch { /* ignore banner load error */ }
        }
        else
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var prev = AccountBannerBitmap;
                AccountBannerBitmap = null;
                prev?.Dispose();
            });
        }
    }

    private static Avalonia.Media.IBrush ResolveBannerBrush(string? bannerColor, IReadOnlyList<string> badges)
    {
        if (!string.IsNullOrWhiteSpace(bannerColor) && bannerColor.StartsWith("#") && bannerColor.Length == 7)
        {
            try
            {
                return Avalonia.Media.Brush.Parse($"#CC{bannerColor.Substring(1)}");
            }
            catch { /* fallback */ }
        }
        return badges.Any(b => b.Contains("Owner", StringComparison.OrdinalIgnoreCase))
            ? Avalonia.Media.Brush.Parse("#2AFBBF24")
            : badges.Any(b => b.Contains("Mod", StringComparison.OrdinalIgnoreCase))
                ? Avalonia.Media.Brush.Parse("#2A5865F2")
                : Avalonia.Media.Brush.Parse("#247BC4A8");
    }

    private static readonly System.Text.RegularExpressions.Regex BioUrlRegex = new(
        @"https?://[^\s<>""']+",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static List<BioTokenItem> ExtractBioLinks(string? bio)
    {
        var list = new List<BioTokenItem>();
        if (string.IsNullOrWhiteSpace(bio)) return list;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (System.Text.RegularExpressions.Match m in BioUrlRegex.Matches(bio))
        {
            var url = m.Value.TrimEnd('.', ',', ';', ')', ']');
            if (seen.Add(url))
                list.Add(new BioTokenItem(url, url, true));
        }
        return list;
    }

    private static List<BioLineItem> ParseBioLines(string? bio)
    {
        var lines = new List<BioLineItem>();
        var text = (bio ?? "").Replace("\r\n", "\n");
        foreach (var rawLine in text.Split('\n'))
        {
            var tokens = new List<BioTokenItem>();
            int pos = 0;
            foreach (System.Text.RegularExpressions.Match m in BioUrlRegex.Matches(rawLine))
            {
                if (m.Index > pos)
                    tokens.Add(new BioTokenItem(rawLine.Substring(pos, m.Index - pos), "", false));
                var url = m.Value.TrimEnd('.', ',', ';', ')', ']');
                tokens.Add(new BioTokenItem(url, url, true));
                pos = m.Index + url.Length;
            }
            if (pos < rawLine.Length)
                tokens.Add(new BioTokenItem(rawLine.Substring(pos), "", false));
            if (tokens.Count == 0)
                tokens.Add(new BioTokenItem(" ", "", false));
            lines.Add(new BioLineItem(tokens));
        }
        return lines;
    }

    [RelayCommand]
    private void ToggleBugReport()
    {
        IsBugReportOpen = !IsBugReportOpen;
        if (IsBugReportOpen)
        {
            BugReportStatus = "";
        }
    }

    [RelayCommand]
    private async Task SubmitBugReportAsync()
    {
        if (IsBugReportSending) return;

        var title = (BugReportTitle ?? "").Trim();
        var description = (BugReportDescription ?? "").Trim();
        var contact = (BugReportContact ?? "").Trim();

        if (title.Length < 3 || description.Length < 10)
        {
            BugReportStatus = L["BugReportValidation"];
            return;
        }

        if (IsReservedAboutContact(contact))
        {
            BugReportStatus = L["BugReportReservedName"];
            return;
        }

        IsBugReportSending = true;
        BugReportStatus = L["BugReportSending"];
        try
        {
            var version = typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "2.0.0";
            var payload = new BugReportPayload(
                title,
                description,
                contact,
                version,
                Environment.OSVersion.VersionString,
                L.Language);

            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, BugReportEndpoint)
            {
                Content = new System.Net.Http.StringContent(
                    JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            var sessionToken = IsAccountSignedIn ? _accountAuth?.GetSessionToken() : null;
            if (!string.IsNullOrWhiteSpace(sessionToken))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sessionToken);

            using var response = await BugReportHttp.SendAsync(req);

            if (response.IsSuccessStatusCode)
            {
                BugReportTitle = "";
                BugReportDescription = "";
                BugReportStatus = L["BugReportSent"];
            }
            else if ((int)response.StatusCode == 403)
            {
                BugReportStatus = L["BugReportReservedName"];
            }
            else if ((int)response.StatusCode == 429)
            {
                BugReportStatus = L["BugReportRateLimited"];
            }
            else
            {
                BugReportStatus = L.Format("BugReportFailed", $"HTTP {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Failed to send bug report", ex);
            BugReportStatus = L.Format("BugReportFailed", ex.Message);
        }
        finally
        {
            IsBugReportSending = false;
        }
    }

    private bool IsReservedAboutContact(string contact)
    {
        var norm = NormalizeContactName(contact);
        if (string.IsNullOrWhiteSpace(norm)) return false;

        var selfNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (IsAccountSignedIn)
        {
            var dn = NormalizeContactName(AccountDisplayName);
            var un = NormalizeContactName(AccountUsername);
            if (!string.IsNullOrWhiteSpace(dn)) selfNames.Add(dn);
            if (!string.IsNullOrWhiteSpace(un)) selfNames.Add(un);
        }
        if (selfNames.Contains(norm)) return false;

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "il dente proibito", "ildenteproibito",
            "shadoweddyx12", "shadoweddyx", "shadow_eddyx",
            "luke", "lukinok", "lukinok9668",
            "koi", "koi | nxinfinity dev", "vibecodegod", "msa_545"
        };
        if (GetAboutMemberNames is not null)
        {
            foreach (var n in GetAboutMemberNames())
            {
                var nn = NormalizeContactName(n);
                if (!string.IsNullOrWhiteSpace(nn) && !selfNames.Contains(nn))
                    reserved.Add(nn);
            }
        }

        foreach (var r in reserved)
        {
            if (selfNames.Contains(r)) continue;
            if (string.Equals(norm, r, StringComparison.OrdinalIgnoreCase) ||
                (r.Length >= 4 && norm.Contains(r, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static string NormalizeContactName(string? value)
        => string.Join(' ', (value ?? "").Trim().TrimStart('@').Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    partial void OnAutoCheckUpdatesChanged(bool value) => Save();

    public async Task CheckForUpdatesSilentAsync()
    {
        if (!AutoCheckUpdates || IsCheckingUpdates || IsDownloadingUpdate) return;
        try
        {
            var (available, manifest) = await AppUpdateService.CheckForUpdatesAsync();
            if (available && manifest is not null)
            {
                _pendingUpdateManifest = manifest;
                IsUpdateAvailable = true;
                UpdateStatus = L.Format("UpdateAvailable", manifest.version);
                UpdateNotes = manifest.notes ?? "";
            }
        }
        catch
        {
            // Startup check is silent on transient network failures
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingUpdates || IsDownloadingUpdate) return;
        IsCheckingUpdates = true;
        UpdateStatus = L["UpdateChecking"];
        UpdateNotes = "";
        try
        {
            var (available, manifest) = await AppUpdateService.CheckForUpdatesAsync();
            if (available && manifest is not null)
            {
                _pendingUpdateManifest = manifest;
                IsUpdateAvailable = true;
                UpdateStatus = L.Format("UpdateAvailable", manifest.version);
                UpdateNotes = manifest.notes ?? "";
            }
            else
            {
                _pendingUpdateManifest = null;
                IsUpdateAvailable = false;
                UpdateStatus = L.Format("UpdateUpToDate", AppUpdateService.CurrentVersion);
            }
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "Update check failed", ex);
            UpdateStatus = L.Format("UpdateFailed", ex.Message);
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdateAsync()
    {
        if (_pendingUpdateManifest is null || IsDownloadingUpdate) return;
        IsDownloadingUpdate = true;
        UpdateProgress = 0;
        UpdateStatus = L["UpdateDownloading"];
        try
        {
            var progress = new Progress<double>(p => UpdateProgress = p);
            await AppUpdateService.DownloadAndApplyAsync(_pendingUpdateManifest, progress);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Error", "Update download/install failed", ex);
            UpdateStatus = L.Format("UpdateFailed", ex.Message);
            IsDownloadingUpdate = false;
        }
    }

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record BugReportPayload(
        string title,
        string description,
        string contact,
        string appVersion,
        string osVersion,
        string language);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record SettingsData(
        bool ShowSession,
        bool ShowAdvice,
        bool ShowSystem,
        bool NightReminder,
        string Preset,
        string? Language = null,
        string? ThemeId = null,
        string? CustomBackgroundPath = null,
        bool LiveStateBridge = false,
        bool RestoreWorkspaceOnReboot = false,
        bool PowerPlanAutopilot = false,
        string? KeepAwakeProcessName = null,
        bool? KeepAwakeBatteryGuard = null,
        bool? AutoCheckUpdates = null,
        double? BackgroundBlur = null,
        double? BackgroundDim = null,
        double? BackgroundVignette = null,
        bool? ShowMascot = null,
        double? MascotPosX = null,
        double? MascotPosY = null,
        bool? HasCompletedTutorial = null,
        bool? DiscordRichPresenceEnabled = null,
        string? DiscordApplicationId = null,
        bool? ShowGlassContainers = null,
        bool? WallpaperFitMode = null,
        List<string>? FavoriteWallpaperIds = null);
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class ProfileBadgeCard
{
    public string Label { get; }
    public Avalonia.Media.IBrush BgBrush { get; }
    public Avalonia.Media.IBrush BorderBrush { get; }
    public Avalonia.Media.IBrush TextBrush { get; }
    public Bitmap? ImageIcon { get; }
    public bool HasImageIcon => ImageIcon is not null;
    public Avalonia.Media.Geometry? VectorPath { get; }
    public bool HasVectorIcon => !HasImageIcon && VectorPath is not null;

    private static readonly Lazy<Bitmap?> OwnerBadgeIcon = new(() => LoadAsset("avares://Uptime/Assets/owner-icon.png"));
    private static readonly Lazy<Bitmap?> ModBadgeIcon = new(() => LoadAsset("avares://Uptime/Assets/mod-icon.png"));

    private static Bitmap? LoadAsset(string uri)
    {
        try
        {
            using var s = Avalonia.Platform.AssetLoader.Open(new Uri(uri));
            return new Bitmap(s);
        }
        catch { return null; }
    }

    public ProfileBadgeCard(string badgeName)
    {
        Label = badgeName;
        var norm = (badgeName ?? "").Trim().ToLowerInvariant();
        if (norm.Contains("owner"))
        {
            BgBrush = Avalonia.Media.Brush.Parse("#24FBBF24");
            BorderBrush = Avalonia.Media.Brush.Parse("#66FBBF24");
            TextBrush = Avalonia.Media.Brush.Parse("#FBBF24");
            ImageIcon = OwnerBadgeIcon.Value;
            VectorPath = null;
        }
        else if (norm.Contains("mod"))
        {
            BgBrush = Avalonia.Media.Brush.Parse("#1F5865F2");
            BorderBrush = Avalonia.Media.Brush.Parse("#665865F2");
            TextBrush = Avalonia.Media.Brush.Parse("#A5B4FC");
            ImageIcon = ModBadgeIcon.Value;
            VectorPath = null;
        }
        else if (norm.Contains("beta"))
        {
            BgBrush = Avalonia.Media.Brush.Parse("#1F38BDF8");
            BorderBrush = Avalonia.Media.Brush.Parse("#6638BDF8");
            TextBrush = Avalonia.Media.Brush.Parse("#38BDF8");
            VectorPath = Avalonia.Media.StreamGeometry.Parse("M9,2V4H10V8.5L5.2,17.2C4.5,18.4 5.4,20 6.8,20H17.2C18.6,20 19.5,18.4 18.8,17.2L14,8.5V4H15V2H9M12,10.5L15.6,17H8.4L12,10.5Z");
        }
        else if (norm.Contains("dev"))
        {
            BgBrush = Avalonia.Media.Brush.Parse("#1FA855F7");
            BorderBrush = Avalonia.Media.Brush.Parse("#73A855F7");
            TextBrush = Avalonia.Media.Brush.Parse("#C084FC");
            VectorPath = Avalonia.Media.StreamGeometry.Parse("M7 2h10a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2zm5 16.5a1 1 0 1 0 0 2 1 1 0 0 0 0-2zM7 5v11h10V5H7z");
        }
        else
        {
            BgBrush = Avalonia.Media.Brush.Parse("#247BC4A8");
            BorderBrush = Avalonia.Media.Brush.Parse("#667BC4A8");
            TextBrush = Avalonia.Media.Brush.Parse("#7BC4A8");
            VectorPath = Avalonia.Media.StreamGeometry.Parse("M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M10,17L5,12L6.41,10.58L10,14.17L17.59,6.58L19,8L10,17Z");
        }
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed partial class ConnectedAccountCard : ObservableObject
{
    public string Key { get; }
    public string Type { get; }
    public string Platform { get; }
    public string Name { get; }
    public string Url { get; }
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
    public Avalonia.Media.Geometry IconPath { get; }
    public Avalonia.Media.IBrush BrandBrush { get; }
    public Avalonia.Media.IBrush BrandBgBrush { get; }
    public Avalonia.Media.IBrush BrandBorderBrush { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibilityIconPath))]
    [NotifyPropertyChangedFor(nameof(VisibilityTooltip))]
    [NotifyPropertyChangedFor(nameof(CardOpacity))]
    private bool _isHidden;

    private static readonly Avalonia.Media.Geometry EyeOffGeo = Avalonia.Media.StreamGeometry.Parse("M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.2 16.38,18.66L16.81,19.08L19.73,22L21,20.73L3.27,3M12,7A5,5 0 0,1 17,12C17,12.64 16.87,13.26 16.64,13.82L19.57,16.75C21.07,15.5 22.27,13.86 23,12C21.27,7.61 17,4.5 12,4.5C10.6,4.5 9.26,4.75 8,5.2L10.17,7.35C10.74,7.13 11.35,7 12,7Z");
    private static readonly Avalonia.Media.Geometry EyeOnGeo = Avalonia.Media.StreamGeometry.Parse("M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5Z");

    public Avalonia.Media.Geometry VisibilityIconPath => IsHidden ? EyeOffGeo : EyeOnGeo;

    public string VisibilityTooltip => IsHidden ? "Show on profile" : "Hide from profile";
    public double CardOpacity => IsHidden ? 0.45 : 1.0;

    public ConnectedAccountCard(ConnectedAccount src)
    {
        Key = $"{src.Type}:{src.Id}";
        Type = (src.Type ?? "").ToLowerInvariant();
        Platform = src.Platform;
        Name = src.Name;
        Url = src.Url;
        _isHidden = src.Hidden;
        var (pathStr, brush, bgHex, borderHex) = GetPlatformBrand(Type);
        IconPath = Avalonia.Media.StreamGeometry.Parse(pathStr);
        BrandBrush = brush;
        BrandBgBrush = Avalonia.Media.Brush.Parse(bgHex);
        BrandBorderBrush = Avalonia.Media.Brush.Parse(borderHex);
    }

    private static (string Path, Avalonia.Media.IBrush Brush, string BgHex, string BorderHex) GetPlatformBrand(string type) => type switch
    {
        "github" => (
            "M12,2A10,10 0 0,0 2,12C2,16.42 4.87,20.17 8.84,21.5C9.34,21.58 9.5,21.27 9.5,21C9.5,20.77 9.5,20.14 9.5,19.31C6.73,19.91 6.14,17.97 6.14,17.97C5.68,16.81 5.03,16.5 5.03,16.5C4.12,15.88 5.1,15.9 5.1,15.9C6.1,15.97 6.63,16.93 6.63,16.93C7.5,18.45 8.97,18 9.54,17.76C9.63,17.11 9.89,16.67 10.17,16.42C7.95,16.17 5.62,15.31 5.62,11.5C5.62,10.39 6,9.5 6.65,8.79C6.55,8.54 6.2,7.5 6.75,6.15C6.75,6.15 7.59,5.88 9.5,7.17C10.29,6.95 11.15,6.84 12,6.84C12.85,6.84 13.71,6.95 14.5,7.17C16.41,5.88 17.25,6.15 17.25,6.15C17.8,7.5 17.45,8.54 17.35,8.79C18,9.5 18.38,10.39 18.38,11.5C18.38,15.32 16.04,16.16 13.81,16.41C14.17,16.72 14.5,17.33 14.5,18.26C14.5,19.6 14.5,20.68 14.5,21C14.5,21.27 14.66,21.59 15.17,21.5C19.14,20.16 22,16.42 22,12A10,10 0 0,0 12,2Z",
            Avalonia.Media.Brush.Parse("#F0F6FC"), "#22F0F6FC", "#44F0F6FC"),
        "steam" => (
            "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22C7.4,22 3.55,18.92 2.36,14.73L6.19,16.31C6.45,17.6 7.6,18.58 8.97,18.58C10.53,18.58 11.8,17.31 11.8,15.75V15.62L15.2,13.19H15.28C17.36,13.19 19.05,11.5 19.05,9.42C19.05,7.34 17.36,5.65 15.28,5.65C13.2,5.65 11.5,7.34 11.5,9.42V9.47L9.13,12.93L8.97,12.92C8.38,12.92 7.83,13.1 7.38,13.41L2,11.2C2.43,6.05 6.73,2 12,2M8.28,17.17C9.08,17.5 10,17.13 10.33,16.33C10.66,15.53 10.28,14.62 9.5,14.28L8.22,13.75C8.71,13.55 9.26,13.53 9.77,13.75C10.34,14 10.77,14.44 11,15C11.24,15.58 11.24,16.21 11,16.78C10.5,17.93 9.21,18.47 8.06,18C7.54,17.78 7.12,17.37 6.89,16.85L8.28,17.17M17.8,9.42C17.8,10.81 16.67,11.94 15.28,11.94C13.9,11.94 12.77,10.81 12.77,9.42A2.52,2.52 0 0,1 15.28,6.91C16.67,6.91 17.8,8.04 17.8,9.42M13.4,9.42C13.4,10.46 14.24,11.3 15.28,11.3C16.32,11.3 17.16,10.46 17.16,9.42C17.16,8.38 16.32,7.54 15.28,7.54C14.24,7.54 13.4,8.38 13.4,9.42Z",
            Avalonia.Media.Brush.Parse("#66C0F4"), "#2266C0F4", "#5566C0F4"),
        "x" or "twitter" => (
            "M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z",
            Avalonia.Media.Brush.Parse("#E7E9EA"), "#22E7E9EA", "#44E7E9EA"),
        "spotify" => (
            "M17.9,10.9C14.7,9 9.35,8.8 6.3,9.75C5.8,9.9 5.3,9.6 5.15,9.15C5,8.65 5.3,8.15 5.75,8C9.3,6.95 15.15,7.15 18.85,9.35C19.3,9.6 19.45,10.2 19.2,10.65C18.95,11 18.35,11.15 17.9,10.9M17.8,13.7C17.55,14.05 17.1,14.2 16.75,13.95C14.05,12.3 9.95,11.8 6.8,12.8C6.4,12.9 5.95,12.7 5.85,12.3C5.75,11.9 5.95,11.45 6.35,11.35C10,10.25 14.5,10.8 17.6,12.7C17.9,12.85 18.05,13.35 17.8,13.7M16.6,16.45C16.4,16.75 16.05,16.85 15.75,16.65C13.4,15.2 10.45,14.9 6.95,15.7C6.6,15.8 6.3,15.55 6.2,15.25C6.1,14.9 6.35,14.6 6.65,14.5C10.45,13.65 13.75,14 16.35,15.6C16.7,15.75 16.75,16.15 16.6,16.45M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z",
            Avalonia.Media.Brush.Parse("#1DB954"), "#221DB954", "#551DB954"),
        "twitch" => (
            "M11.64 5.93h1.43v4.28h-1.43m3.93-4.28H17v4.28h-1.43M7 2L3.43 5.57v12.86h4.28V22l3.58-3.57h2.85L20.57 12V2m-1.43 9.29l-2.85 2.85h-2.86l-2.5 2.5v-2.5H7.71V3.43h11.43Z",
            Avalonia.Media.Brush.Parse("#9146FF"), "#229146FF", "#559146FF"),
        "youtube" => (
            "M10,15L15.19,12L10,9V15M21.56,7.17C21.69,7.64 21.78,8.27 21.84,9.07C21.91,9.87 21.94,10.56 21.94,11.16L22,12C22,14.19 21.84,15.8 21.56,16.83C21.31,17.73 20.73,18.31 19.83,18.56C19.36,18.69 18.5,18.78 17.18,18.84C15.88,18.91 14.69,18.94 13.59,18.94L12,19C7.81,19 5.2,18.84 4.17,18.56C3.27,18.31 2.69,17.73 2.44,16.83C2.31,16.36 2.22,15.73 2.16,14.93C2.09,14.13 2.06,13.44 2.06,12.84L2,12C2,9.81 2.16,8.2 2.44,7.17C2.69,6.27 3.27,5.69 4.17,5.44C4.64,5.31 5.5,5.22 6.82,5.16C8.12,5.09 9.31,5.06 10.41,5.06L12,5C16.19,5 18.8,5.16 19.83,5.44C20.73,5.69 21.31,6.27 21.56,7.17Z",
            Avalonia.Media.Brush.Parse("#FF4444"), "#22FF4444", "#55FF4444"),
        _ => (
            "M10.59,13.41C11,13.8 11,14.44 10.59,14.83C10.2,15.22 9.56,15.22 9.17,14.83C7.22,12.88 7.22,9.71 9.17,7.76L12.71,4.22C14.66,2.27 17.83,2.27 19.78,4.22C21.73,6.17 21.73,9.34 19.78,11.29L18.29,12.78C18.3,11.96 18.17,11.14 17.89,10.36L18.36,9.89C19.54,8.71 19.54,6.81 18.36,5.64C17.19,4.46 15.29,4.46 14.11,5.64L10.59,9.17C9.41,10.34 9.41,12.24 10.59,13.41M13.41,9.17C13.8,8.78 14.44,8.78 14.83,9.17C16.78,11.12 16.78,14.29 14.83,16.24L11.29,19.78C9.34,21.73 6.17,21.73 4.22,19.78C2.27,17.83 2.27,14.66 4.22,12.71L5.71,11.22C5.7,12.04 5.83,12.86 6.11,13.64L5.64,14.11C4.46,15.29 4.46,17.19 5.64,18.36C6.81,19.54 8.71,19.54 9.89,18.36L13.41,14.83C14.59,13.66 14.59,11.76 13.41,10.59C13,10.2 13,9.56 13.41,9.17Z",
            Avalonia.Media.Brush.Parse("#7BC4A8"), "#227BC4A8", "#557BC4A8"),
    };
}

public sealed partial class CommunityWallpaperCard : ObservableObject
{
    private static readonly System.Net.Http.HttpClient ThumbHttp = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly SemaphoreSlim NetGate = new(8, 8);
    private static readonly SemaphoreSlim DecodeGate = new(4, 4);
    private static readonly string ThumbCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "thumb-cache");

    public CommunityWallpaperItem Item { get; }
    public string Title => Item.Title;
    public string Author => Item.Author;
    public string Resolution => Item.Resolution;

    public string AspectBadge
    {
        get
        {
            if (!SettingsViewModel.TryParseResolution(Resolution, out var w, out var h)) return "";
            var ratio = (double)w / h;
            if (ratio >= 2.8) return "32:9";
            if (ratio >= 2.05) return "21:9";
            return "";
        }
    }

    public bool IsUltrawide => !string.IsNullOrEmpty(AspectBadge);

    private static readonly Lazy<Bitmap?> OwnerIconLazy = new(() => LoadRoleIcon("avares://Uptime/Assets/owner-icon.png"));
    private static readonly Lazy<Bitmap?> ModIconLazy = new(() => LoadRoleIcon("avares://Uptime/Assets/mod-icon.png"));

    public Bitmap? RoleIcon
    {
        get
        {
            var norm = string.Join(' ', (Author ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return norm switch
            {
                "il dente proibito" => OwnerIconLazy.Value,
                "shadoweddyx12" or "luke" => ModIconLazy.Value,
                _ => null
            };
        }
    }

    public bool HasRoleIcon => RoleIcon is not null;
    public string RoleTooltip
    {
        get
        {
            var norm = string.Join(' ', (Author ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
            return norm == "il dente proibito" ? "Owner" : norm is "shadoweddyx12" or "luke" ? "Moderator" : "";
        }
    }

    private static Bitmap? LoadRoleIcon(string uri)
    {
        try
        {
            using var stream = Avalonia.Platform.AssetLoader.Open(new Uri(uri));
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FavoriteColor))]
    private bool _isFavorite;
    public Avalonia.Media.IBrush FavoriteColor => IsFavorite
        ? Avalonia.Media.Brush.Parse("#F43F5E")
        : Avalonia.Media.Brush.Parse("#94A3B8");

    public CommunityWallpaperCard(CommunityWallpaperItem item, Bitmap? cachedThumbnail = null)
    {
        Item = item;
        _thumbnail = cachedThumbnail;
    }

    private static string GetCachePath(string id)
    {
        var safe = string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
        if (string.IsNullOrEmpty(safe)) safe = Math.Abs(id.GetHashCode()).ToString();
        return Path.Combine(ThumbCacheDir, $"{safe}.png");
    }

    public async Task LoadThumbnailAsync()
    {
        if (Thumbnail is not null || string.IsNullOrWhiteSpace(Item.ImageUrl)) return;

        var cachePath = GetCachePath(Item.Id);
        if (File.Exists(cachePath))
        {
            await DecodeGate.WaitAsync();
            try
            {
                if (Thumbnail is not null) return;
                var cachedBmp = await Task.Run(() =>
                {
                    using var fs = File.OpenRead(cachePath);
                    return new Bitmap(fs);
                });
                Thumbnail = cachedBmp;
                return;
            }
            catch
            {
                // Corrupt cache file; fall through to network fetch
            }
            finally
            {
                DecodeGate.Release();
            }
        }

        byte[] bytes;
        await NetGate.WaitAsync();
        try
        {
            if (Thumbnail is not null) return;
            bytes = await ThumbHttp.GetByteArrayAsync(Item.ImageUrl);
        }
        catch
        {
            return;
        }
        finally
        {
            NetGate.Release();
        }

        await DecodeGate.WaitAsync();
        try
        {
            if (Thumbnail is not null) return;
            var bmp = await Task.Run(() =>
            {
                using var ms = new MemoryStream(bytes);
                var decoded = Bitmap.DecodeToWidth(ms, 488);
                try
                {
                    Directory.CreateDirectory(ThumbCacheDir);
                    var tmp = $"{cachePath}.{Guid.NewGuid():N}.tmp";
                    try
                    {
                        decoded.Save(tmp);
                        File.Move(tmp, cachePath, overwrite: true);
                    }
                    finally
                    {
                        if (File.Exists(tmp)) try { File.Delete(tmp); } catch { }
                    }
                }
                catch
                {
                    // Cache write is best-effort
                }
                return decoded;
            });
            Thumbnail = bmp;
        }
        catch
        {
            // Ignore transient thumbnail decode errors
        }
        finally
        {
            DecodeGate.Release();
        }
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class BioTokenItem
{
    public string Text { get; }
    public string Url { get; }
    public bool IsLink { get; }
    public bool IsPlain => !IsLink;

    public BioTokenItem(string text, string url, bool isLink)
    {
        Text = text;
        Url = url;
        IsLink = isLink;
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class BioLineItem
{
    public IReadOnlyList<BioTokenItem> Tokens { get; }

    public BioLineItem(IReadOnlyList<BioTokenItem> tokens)
    {
        Tokens = tokens;
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed partial class FriendCardItem : ObservableObject, IDisposable
{
    private static readonly System.Net.Http.HttpClient AvatarHttp = new() { Timeout = TimeSpan.FromSeconds(8) };

    public string DiscordId { get; }
    public string Username { get; }
    public string DisplayName { get; }
    public string UsernameLabel { get; }
    public string AvatarUrl { get; }
    public string CustomStatus { get; }
    public bool HasStatus { get; }
    public ProfileBadgeCard? PrimaryBadge { get; }
    public bool HasPrimaryBadge => PrimaryBadge is not null;

    [ObservableProperty] private Bitmap? _avatarBitmap;

    public FriendCardItem(FriendProfileSummary summary)
    {
        DiscordId = summary.DiscordId;
        Username = summary.Username;
        DisplayName = string.IsNullOrWhiteSpace(summary.DisplayName) ? summary.Username : summary.DisplayName;
        UsernameLabel = string.IsNullOrWhiteSpace(summary.Username) ? "" : $"@{summary.Username}";
        AvatarUrl = summary.AvatarUrl;
        CustomStatus = summary.CustomStatus;
        HasStatus = !string.IsNullOrWhiteSpace(summary.CustomStatus);
        var topBadge = summary.Badges.FirstOrDefault();
        PrimaryBadge = string.IsNullOrWhiteSpace(topBadge) ? null : new ProfileBadgeCard(topBadge);
    }

    public async Task LoadAvatarAsync()
    {
        if (AvatarBitmap is not null || string.IsNullOrWhiteSpace(AvatarUrl)) return;
        try
        {
            var bytes = await AvatarHttp.GetByteArrayAsync(AvatarUrl);
            using var ms = new MemoryStream(bytes);
            var bmp = await Task.Run(() => Bitmap.DecodeToWidth(ms, 96));
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => AvatarBitmap = bmp);
        }
        catch { /* ignore */ }
    }

    public void Dispose()
    {
        AvatarBitmap?.Dispose();
        AvatarBitmap = null;
    }
}

[System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed class FriendChatBubbleItem
{
    public string Text { get; }
    public string TimeLabel { get; }
    public bool IsMine { get; }
    public Avalonia.Layout.HorizontalAlignment BubbleAlignment { get; }
    public Avalonia.Media.IBrush BubbleBgBrush { get; }
    public Avalonia.Media.IBrush BubbleBorderBrush { get; }
    public Avalonia.Media.IBrush TextBrush { get; }

    public FriendChatBubbleItem(FriendChatMessage msg, bool isMine)
    {
        Text = msg.Text;
        TimeLabel = msg.Timestamp == default ? "" : msg.Timestamp.ToLocalTime().ToString("HH:mm");
        IsMine = isMine;
        BubbleAlignment = isMine ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
        BubbleBgBrush = Avalonia.Media.Brush.Parse(isMine ? "#247BC4A8" : "#18212E");
        BubbleBorderBrush = Avalonia.Media.Brush.Parse(isMine ? "#557BC4A8" : "#26FFFFFF");
        TextBrush = Avalonia.Media.Brush.Parse(isMine ? "#ECFFF7" : "#E2E8F0");
    }
}
