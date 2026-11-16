using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Uptime.Core;

namespace Uptime.Presentation.ViewModels;

public sealed partial class NewsViewModel : ObservableObject
{
    private readonly INewsService _newsService;
    private readonly SettingsViewModel _settings;
    private static Localizer L => Localizer.Instance;

    public SettingsViewModel Settings => _settings;
    public bool IsOwner => _settings.IsOwner;
    public bool IsAccountSignedIn => _settings.IsAccountSignedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticles), nameof(ShowEmptyFeedState))]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedArticle))]
    private NewsArticleItemViewModel? _selectedArticle;

    public bool HasArticles => Articles.Count > 0;
    public bool ShowEmptyFeedState => !IsLoading && Articles.Count == 0;
    public bool HasSelectedArticle => SelectedArticle is not null;

    [ObservableProperty]
    private string _categoryFilter = "ALL";

    public ObservableCollection<NewsArticleItemViewModel> Articles { get; } = new();
    public ObservableCollection<NewsArticleItemViewModel> FilteredArticles { get; } = new();
    public ObservableCollection<NewsCommentItemViewModel> Comments { get; } = new();
    public ObservableCollection<ReactionChipViewModel> ReactionBar { get; } = new();

    // Comment Composer
    [ObservableProperty]
    private string _newCommentText = "";

    [ObservableProperty]
    private bool _isSendingComment;

    [ObservableProperty]
    private string _commentStatus = "";

    // Admin Publishing Panel (Strictly Owner)
    [ObservableProperty]
    private bool _isAdminOpen;

    [ObservableProperty]
    private string _adminTitle = "";

    [ObservableProperty]
    private string _adminContent = "";

    [ObservableProperty]
    private string _adminCategory = "UPDATE";

    [ObservableProperty]
    private bool _adminIsPinned;

    [ObservableProperty]
    private string _adminCoverImageUrl = "";

    [ObservableProperty]
    private Bitmap? _adminCoverImagePreview;

    [ObservableProperty]
    private string _adminKey = "";

    [ObservableProperty]
    private string _adminStatus = "";

    [ObservableProperty]
    private bool _isAdminBusy;

    public NewsViewModel(INewsService newsService, SettingsViewModel settings)
    {
        _newsService = newsService;
        _settings = settings;

        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(SettingsViewModel.IsAccountSignedIn) or nameof(SettingsViewModel.IsOwner))
            {
                OnPropertyChanged(nameof(IsOwner));
                OnPropertyChanged(nameof(IsAccountSignedIn));
                RefreshReactionBar();
            }
        };

        SetupDefaultReactions();
        _ = LoadNewsAsync();
    }

    private void SetupDefaultReactions()
    {
        ReactionBar.Clear();
        var defaultEmojis = new[] { "🔥", "🚀", "👀", "❤️", "⚡", "🎉", "💯", "👏" };
        foreach (var emoji in defaultEmojis)
        {
            ReactionBar.Add(new ReactionChipViewModel(emoji, 0, false, OnToggleReaction));
        }
    }

    private void RefreshReactionBar()
    {
        if (SelectedArticle == null) return;
        var myReactions = new HashSet<string>(SelectedArticle.Article.MyReactions ?? Enumerable.Empty<string>());
        var counts = SelectedArticle.Article.Reactions ?? new Dictionary<string, int>();

        foreach (var chip in ReactionBar)
        {
            counts.TryGetValue(chip.Emoji, out var count);
            chip.Count = count;
            chip.HasUserReacted = myReactions.Contains(chip.Emoji);
        }
    }

    [RelayCommand]
    public async Task LoadNewsAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        StatusMessage = "";

        try
        {
            var token = _settings.GetAccountSessionToken();
            var items = await _newsService.GetNewsFeedAsync(token).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Articles.Clear();
                foreach (var item in items)
                {
                    Articles.Add(new NewsArticleItemViewModel(item));
                }

                ApplyCategoryFilter();

                // Select first article or retain existing
                var match = SelectedArticle != null
                    ? FilteredArticles.FirstOrDefault(a => a.Id == SelectedArticle.Id)
                    : null;
                SelectArticle(match ?? FilteredArticles.FirstOrDefault());
            });
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public void SetCategoryFilter(string? category)
    {
        CategoryFilter = string.IsNullOrWhiteSpace(category) ? "ALL" : category.Trim().ToUpperInvariant();
        ApplyCategoryFilter();
    }

    private void ApplyCategoryFilter()
    {
        FilteredArticles.Clear();
        var filter = CategoryFilter;
        var filtered = (filter == "ALL")
            ? Articles
            : Articles.Where(a => string.Equals(a.Category, filter, StringComparison.OrdinalIgnoreCase));

        foreach (var item in filtered)
        {
            FilteredArticles.Add(item);
        }

        if (SelectedArticle != null && !FilteredArticles.Contains(SelectedArticle))
        {
            SelectArticle(FilteredArticles.FirstOrDefault());
        }
    }

    [RelayCommand]
    public void SelectArticle(NewsArticleItemViewModel? article)
    {
        foreach (var a in Articles) a.IsSelected = false;
        SelectedArticle = article;

        if (article != null)
        {
            article.IsSelected = true;
            RefreshReactionBar();
            _ = LoadArticleDetailsAsync(article.Id);
        }
        else
        {
            Comments.Clear();
        }
    }

    private async Task LoadArticleDetailsAsync(string newsId)
    {
        try
        {
            var token = _settings.GetAccountSessionToken();
            var (article, comments) = await _newsService.GetNewsDetailsAsync(newsId, token).ConfigureAwait(false);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (SelectedArticle != null && SelectedArticle.Id == newsId)
                {
                    if (article != null)
                    {
                        SelectedArticle.Update(article);
                        RefreshReactionBar();
                    }

                    Comments.Clear();
                    foreach (var c in comments)
                    {
                        Comments.Add(new NewsCommentItemViewModel(c, DeleteCommentCommand));
                    }
                }
            });
        }
        catch
        {
            // Retain cached data on temporary network glitches
        }
    }

    private async void OnToggleReaction(string emoji)
    {
        if (SelectedArticle == null || !_settings.IsAccountSignedIn) return;
        var token = _settings.GetAccountSessionToken();
        if (string.IsNullOrWhiteSpace(token)) return;

        var chip = ReactionBar.FirstOrDefault(c => c.Emoji == emoji);
        if (chip == null) return;

        // Optimistic UI toggle
        var wasReacted = chip.HasUserReacted;
        chip.HasUserReacted = !wasReacted;
        chip.Count = Math.Max(0, chip.Count + (wasReacted ? -1 : 1));

        var ok = await _newsService.ToggleReactionAsync(SelectedArticle.Id, emoji, token).ConfigureAwait(false);
        if (!ok)
        {
            // Rollback on failure
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                chip.HasUserReacted = wasReacted;
                chip.Count = Math.Max(0, chip.Count + (wasReacted ? 1 : -1));
            });
        }
    }

    [RelayCommand]
    public async Task SendCommentAsync()
    {
        if (SelectedArticle == null || !_settings.IsAccountSignedIn || IsSendingComment) return;
        var text = (NewCommentText ?? "").Trim();
        if (text.Length < 2) return;

        var token = _settings.GetAccountSessionToken();
        if (string.IsNullOrWhiteSpace(token)) return;

        IsSendingComment = true;
        CommentStatus = "";

        try
        {
            var created = await _newsService.AddCommentAsync(SelectedArticle.Id, text, token).ConfigureAwait(false);
            if (created != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Comments.Add(new NewsCommentItemViewModel(created, DeleteCommentCommand));
                    NewCommentText = "";
                    if (SelectedArticle != null)
                    {
                        SelectedArticle.CommentsCount = Comments.Count;
                    }
                });
            }
            else
            {
                CommentStatus = "Errore durante l'invio del commento.";
            }
        }
        catch (Exception ex)
        {
            CommentStatus = ex.Message;
        }
        finally
        {
            IsSendingComment = false;
        }
    }

    [RelayCommand]
    public async Task DeleteCommentAsync(NewsCommentItemViewModel? comment)
    {
        if (comment == null) return;
        var token = _settings.GetAccountSessionToken();
        if (string.IsNullOrWhiteSpace(token)) return;

        var ok = await _newsService.DeleteCommentAsync(comment.Id, token).ConfigureAwait(false);
        if (ok)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                Comments.Remove(comment);
                if (SelectedArticle != null)
                {
                    SelectedArticle.CommentsCount = Math.Max(0, Comments.Count);
                }
            });
        }
    }

    private static readonly HttpClient PreviewHttp = new() { Timeout = TimeSpan.FromSeconds(8) };
    private CancellationTokenSource? _coverPreviewCts;

    partial void OnAdminCoverImageUrlChanged(string value)
    {
        _ = UpdateAdminCoverPreviewAsync(value);
    }

    private async Task UpdateAdminCoverPreviewAsync(string? url)
    {
        _coverPreviewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _coverPreviewCts = cts;

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var old = AdminCoverImagePreview;
                AdminCoverImagePreview = null;
                old?.Dispose();
            });
            return;
        }

        try
        {
            await Task.Delay(350, cts.Token).ConfigureAwait(false);
            var bytes = await PreviewHttp.GetByteArrayAsync(uri, cts.Token).ConfigureAwait(false);
            if (bytes is { Length: > 0 } && !cts.Token.IsCancellationRequested)
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new Bitmap(ms);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (cts.Token.IsCancellationRequested)
                    {
                        bmp.Dispose();
                        return;
                    }
                    var old = AdminCoverImagePreview;
                    AdminCoverImagePreview = bmp;
                    old?.Dispose();
                });
            }
        }
        catch
        {
            // Ignore cancellation or invalid remote image
        }
    }

    public async Task SetCoverImageFromLocalPathAsync(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath)) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(localPath).ConfigureAwait(false);
            if (bytes.Length > 3_100_000)
            {
                AdminStatus = "Immagine troppo grande (max 3 MB).";
                return;
            }

            Bitmap bmp;
            using (var ms = new MemoryStream(bytes))
                bmp = new Bitmap(ms);

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var old = AdminCoverImagePreview;
                AdminCoverImagePreview = bmp;
                old?.Dispose();
            });

            var token = _settings.GetAccountSessionToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                AdminStatus = "Sessione non valida. Effettua nuovamente l'accesso.";
                return;
            }

            AdminStatus = "Caricamento thumbnail...";
            var uploadedUrl = await _newsService.UploadCoverImageAsync(bytes, token, AdminKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(uploadedUrl))
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AdminCoverImageUrl = uploadedUrl;
                    AdminStatus = "";
                });
            }
            else
            {
                AdminStatus = "Inserisci prima la Master Admin Key valida per caricare da file, oppure incolla un URL diretto.";
            }
        }
        catch (Exception ex)
        {
            AdminStatus = "Impossibile caricare l'immagine: " + ex.Message;
        }
    }

    // -------------------------------------------------------------------------
    // Admin Panel Actions (Strictly Owner-Protected)
    // -------------------------------------------------------------------------
    [RelayCommand]
    public void OpenAdminPanel()
    {
        if (!_settings.IsOwner) return;
        AdminTitle = "";
        AdminContent = "";
        AdminCategory = "UPDATE";
        AdminIsPinned = false;
        AdminCoverImageUrl = "";
        AdminCoverImagePreview?.Dispose();
        AdminCoverImagePreview = null;
        AdminStatus = "";
        IsAdminOpen = true;
    }

    [RelayCommand]
    public void CloseAdminPanel()
    {
        IsAdminOpen = false;
    }

    [RelayCommand]
    public async Task PublishNewsAsync()
    {
        if (!_settings.IsOwner || IsAdminBusy) return;
        var title = (AdminTitle ?? "").Trim();
        var content = (AdminContent ?? "").Trim();
        if (title.Length < 3)
        {
            AdminStatus = "Il titolo deve contenere almeno 3 caratteri.";
            return;
        }
        if (content.Length < 10)
        {
            AdminStatus = "Il contenuto della notizia deve contenere almeno 10 caratteri.";
            return;
        }

        var token = _settings.GetAccountSessionToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            AdminStatus = "Sessione non valida. Effettua nuovamente l'accesso.";
            return;
        }

        IsAdminBusy = true;
        AdminStatus = "";

        try
        {
            var coverUrl = string.IsNullOrWhiteSpace(AdminCoverImageUrl) ? null : AdminCoverImageUrl.Trim();
            var created = await _newsService.PublishNewsAsync(
                title,
                content,
                AdminCategory,
                AdminIsPinned,
                coverImageUrl: coverUrl,
                token,
                adminKey: AdminKey,
                ct: CancellationToken.None).ConfigureAwait(false);

            if (created != null)
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var itemVm = new NewsArticleItemViewModel(created);
                    Articles.Insert(0, itemVm);
                    ApplyCategoryFilter();
                    SelectArticle(itemVm);
                    IsAdminOpen = false;
                });
            }
            else
            {
                AdminStatus = "Errore durante la pubblicazione. Verifica la Master Admin Key o i permessi Owner.";
            }
        }
        catch (Exception ex)
        {
            AdminStatus = ex.Message;
        }
        finally
        {
            IsAdminBusy = false;
        }
    }

    [RelayCommand]
    public async Task DeleteCurrentNewsAsync()
    {
        if (SelectedArticle == null || !_settings.IsOwner) return;
        var token = _settings.GetAccountSessionToken();
        if (string.IsNullOrWhiteSpace(token)) return;

        var ok = await _newsService.DeleteNewsAsync(SelectedArticle.Id, token, adminKey: AdminKey).ConfigureAwait(false);
        if (ok)
        {
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                var toRemove = SelectedArticle;
                Articles.Remove(toRemove);
                FilteredArticles.Remove(toRemove);
                SelectArticle(FilteredArticles.FirstOrDefault());
            });
        }
    }
}

public sealed partial class NewsArticleItemViewModel : ObservableObject
{
    private NewsArticle _article;
    public NewsArticle Article => _article;

    public string Id => _article.Id;
    public string Title => _article.Title;
    public string Content => _article.Content;
    public string Category => _article.Category;
    public bool IsPinned => _article.IsPinned;
    public string AuthorName => _article.AuthorName;
    public string AuthorAvatar => _article.AuthorAvatar;
    public string CoverImageUrl => _article.CoverImageUrl;
    public bool HasCoverImage => !string.IsNullOrWhiteSpace(_article.CoverImageUrl);
    public DateTimeOffset CreatedAt => _article.CreatedAt;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _commentsCount;

    [ObservableProperty]
    private Bitmap? _thumbnail;

    private static readonly HttpClient ThumbHttp = new() { Timeout = TimeSpan.FromSeconds(10) };

    public async Task LoadThumbnailAsync()
    {
        if (Thumbnail != null || string.IsNullOrWhiteSpace(_article.CoverImageUrl)) return;
        try
        {
            var bytes = await ThumbHttp.GetByteArrayAsync(_article.CoverImageUrl).ConfigureAwait(false);
            if (bytes is { Length: > 0 })
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new Bitmap(ms);
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => Thumbnail = bmp);
            }
        }
        catch
        {
            // Silently ignore failed remote thumbnails
        }
    }

    public string FormattedDate => FormatRelativeDate(_article.CreatedAt);
    public string FormattedFullDate => _article.CreatedAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm");

    public NewsArticleItemViewModel(NewsArticle article)
    {
        _article = article;
        _commentsCount = article.CommentsCount;
        if (HasCoverImage)
        {
            _ = LoadThumbnailAsync();
        }
    }

    public void Update(NewsArticle article)
    {
        _article = article;
        CommentsCount = article.CommentsCount;
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Content));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(CoverImageUrl));
        OnPropertyChanged(nameof(HasCoverImage));
        OnPropertyChanged(nameof(FormattedDate));
        OnPropertyChanged(nameof(FormattedFullDate));
        if (HasCoverImage && Thumbnail == null)
        {
            _ = LoadThumbnailAsync();
        }
    }

    private static string FormatRelativeDate(DateTimeOffset dt)
    {
        var span = DateTimeOffset.UtcNow - dt;
        if (span.TotalMinutes < 1) return "Adesso";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m fa";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h fa";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays}g fa";
        return dt.ToLocalTime().ToString("dd MMM yyyy");
    }
}

public sealed partial class NewsCommentItemViewModel : ObservableObject
{
    private readonly NewsComment _comment;
    private readonly IRelayCommand<NewsCommentItemViewModel> _deleteCommand;

    public string Id => _comment.Id;
    public string NewsId => _comment.NewsId;
    public string DiscordId => _comment.DiscordId;
    public string DisplayName => _comment.DisplayName;
    public string Username => string.IsNullOrWhiteSpace(_comment.Username) ? "" : $"@{_comment.Username}";
    public string Content => _comment.Content;
    public bool CanDelete => _comment.CanDelete;
    public List<string> Badges => _comment.Badges;
    public string FormattedDate => _comment.CreatedAt.ToLocalTime().ToString("dd MMM, HH:mm");

    public IRelayCommand DeleteCommand => new RelayCommand(() => _deleteCommand.Execute(this));

    public NewsCommentItemViewModel(NewsComment comment, IRelayCommand<NewsCommentItemViewModel> deleteCommand)
    {
        _comment = comment;
        _deleteCommand = deleteCommand;
    }
}

public sealed partial class ReactionChipViewModel : ObservableObject
{
    private readonly Action<string> _toggleAction;

    public string Emoji { get; }

    [ObservableProperty]
    private int _count;

    [ObservableProperty]
    private bool _hasUserReacted;

    public IRelayCommand ToggleCommand => new RelayCommand(() => _toggleAction(Emoji));

    public ReactionChipViewModel(string emoji, int count, bool hasUserReacted, Action<string> toggleAction)
    {
        Emoji = emoji;
        _count = count;
        _hasUserReacted = hasUserReacted;
        _toggleAction = toggleAction;
    }
}
