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

public partial class SettingsViewModel : ObservableObject
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

    [ObservableProperty] private bool _isBugReportOpen;
    [ObservableProperty] private string _bugReportTitle = "";
    [ObservableProperty] private string _bugReportDescription = "";
    [ObservableProperty] private string _bugReportContact = "";
    [ObservableProperty] private string _bugReportStatus = "";
    [ObservableProperty] private bool _isBugReportSending;

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
    private readonly bool _isLoading;

    public SettingsViewModel(ICommunityWallpaperService? communityWallpapers = null)
    {
        _communityWallpapers = communityWallpapers;
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
                    }
                    catch (Exception ex)
                    {
                        AppDiagnostics.Write("Warning", "Could not load saved custom background", ex);
                    }
                }

                ShowSession = saved.ShowSession; ShowAdvice = saved.ShowAdvice; ShowSystem = saved.ShowSystem;
                ShowMascot = saved.ShowMascot ?? true;
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
        _isLoading = false;
        Save();
    }

    private void OnLocalizerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(Localizer.Language)) return;
        OnPropertyChanged(nameof(IsItalian));
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsRussian));
        OnPropertyChanged(nameof(LanguageIndicatorOffset));
        OnPropertyChanged(nameof(CurrentVersionLabel));
        OnPropertyChanged(nameof(L));
        Save();
    }

    partial void OnShowSessionChanged(bool value) => Save();
    partial void OnShowAdviceChanged(bool value) => Save();
    partial void OnShowSystemChanged(bool value) => Save();
    partial void OnShowMascotChanged(bool value) => Save();

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

    /// <summary>
    /// Decodes a custom background image capped to 2560px width so 4K/8K wallpapers
    /// do not force expensive per-frame resampling during UI scrolling.
    /// </summary>
    private static Bitmap LoadBackgroundBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        return Bitmap.DecodeToWidth(stream, 2560, BitmapInterpolationMode.MediumQuality);
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
            _baseCommunityCards.Clear();
            _communityLoopCursor = 0;
            CommunityWallpapers.Clear();
            foreach (var item in items)
            {
                var card = new CommunityWallpaperCard(item);
                _baseCommunityCards.Add(card);
                CommunityWallpapers.Add(card);
                _ = card.LoadThumbnailAsync();
            }
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
            CommunityWallpapers.Insert(0, card);
            _ = card.LoadThumbnailAsync();
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
        if (_baseCommunityCards.Count == 0 || CommunityWallpapers.Count >= 600) return;
        var count = Math.Min(batchSize, _baseCommunityCards.Count);
        for (var i = 0; i < count; i++)
        {
            var src = _baseCommunityCards[_communityLoopCursor % _baseCommunityCards.Count];
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
                return reusedPreview ?? LoadBackgroundBitmap(destPath);
            });

            var previous = CustomBackgroundImage;
            CustomBackgroundPath = destPath;
            CustomBackgroundImage = bmp;
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
            PreviewingFullImage = null;
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

    [RelayCommand]
    private async Task PreviewCommunityWallpaperAsync(CommunityWallpaperCard? card)
    {
        if (card is null) return;
        PreviewingFullImage = null;
        PreviewingWallpaper = card;
        IsStudioOpen = true;

        if (_communityWallpapers is null) return;
        try
        {
            var localPath = await _communityWallpapers.DownloadWallpaperToTempAsync(card.Item, countDownload: false);
            if (PreviewingWallpaper == card && IsStudioOpen && File.Exists(localPath))
            {
                var fullBmp = await Task.Run(() => LoadBackgroundBitmap(localPath));
                if (PreviewingWallpaper == card && IsStudioOpen)
                    PreviewingFullImage = fullBmp;
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
        PreviewingFullImage = null;
        PreviewingWallpaper = null;
        IsStudioOpen = true;
    }

    [RelayCommand]
    private void CloseWallpaperPreview()
    {
        IsStudioOpen = false;
        PreviewingWallpaper = null;
        PreviewingFullImage = null;
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
                DiscordRichPresenceEnabled, DiscordApplicationId)));
            File.Move(tmp, FilePath, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
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

            using var content = new System.Net.Http.StringContent(
                JsonSerializer.Serialize(payload),
                System.Text.Encoding.UTF8,
                "application/json");
            using var response = await BugReportHttp.PostAsync(BugReportEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                BugReportTitle = "";
                BugReportDescription = "";
                BugReportStatus = L["BugReportSent"];
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
        string? DiscordApplicationId = null);
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
