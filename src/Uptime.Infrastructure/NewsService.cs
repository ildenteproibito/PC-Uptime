using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class NewsService : INewsService
{
    private const string Endpoint = "https://uptime-pc.vercel.app/api/news";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? sessionToken = null, string? adminKey = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(sessionToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken.Trim());
        }
        if (!string.IsNullOrWhiteSpace(adminKey))
        {
            req.Headers.Add("x-admin-key", adminKey.Trim());
        }
        return req;
    }

    public async Task<IReadOnlyList<NewsArticle>> GetNewsFeedAsync(string? sessionToken = null, CancellationToken ct = default)
    {
        try
        {
            using var req = CreateRequest(HttpMethod.Get, Endpoint, sessionToken);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return Array.Empty<NewsArticle>();

            var payload = await resp.Content.ReadFromJsonAsync<NewsFeedResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            if (payload?.News is null) return Array.Empty<NewsArticle>();

            return payload.News
                .Where(n => !string.IsNullOrWhiteSpace(n.Id) && !string.IsNullOrWhiteSpace(n.Title))
                .Select(ToModel)
                .ToList();
        }
        catch
        {
            return Array.Empty<NewsArticle>();
        }
    }

    public async Task<(NewsArticle? Article, IReadOnlyList<NewsComment> Comments)> GetNewsDetailsAsync(
        string newsId,
        string? sessionToken = null,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newsId)) return (null, Array.Empty<NewsComment>());
            var url = $"{Endpoint}?id={Uri.EscapeDataString(newsId.Trim())}";
            using var req = CreateRequest(HttpMethod.Get, url, sessionToken);
            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return (null, Array.Empty<NewsComment>());

            var payload = await resp.Content.ReadFromJsonAsync<NewsDetailsResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            if (payload?.News is null) return (null, Array.Empty<NewsComment>());

            var article = ToModel(payload.News);
            var comments = (payload.Comments ?? new List<NewsCommentDto>())
                .Select(ToCommentModel)
                .ToList();

            return (article, comments);
        }
        catch
        {
            return (null, Array.Empty<NewsComment>());
        }
    }

    public async Task<bool> ToggleReactionAsync(
        string newsId,
        string emoji,
        string sessionToken,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newsId) || string.IsNullOrWhiteSpace(emoji) || string.IsNullOrWhiteSpace(sessionToken))
                return false;

            var body = new { action = "react", newsId = newsId.Trim(), emoji = emoji.Trim() };
            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<NewsComment?> AddCommentAsync(
        string newsId,
        string content,
        string sessionToken,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newsId) || string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(sessionToken))
                return null;

            var body = new { action = "comment", newsId = newsId.Trim(), content = content.Trim() };
            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var payload = await resp.Content.ReadFromJsonAsync<CommentActionResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            return payload?.Comment is not null ? ToCommentModel(payload.Comment) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteCommentAsync(
        string commentId,
        string sessionToken,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(commentId) || string.IsNullOrWhiteSpace(sessionToken))
                return false;

            var body = new { action = "delete_comment", commentId = commentId.Trim() };
            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<NewsArticle?> PublishNewsAsync(
        string title,
        string content,
        string category,
        bool isPinned,
        string? coverImageUrl,
        string sessionToken,
        string? adminKey,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(sessionToken))
                return null;

            var body = new
            {
                action = "publish",
                title = title.Trim(),
                content = content.Trim(),
                category = string.IsNullOrWhiteSpace(category) ? "UPDATE" : category.Trim().ToUpperInvariant(),
                isPinned,
                coverImageUrl = coverImageUrl?.Trim() ?? "",
                adminKey = adminKey?.Trim() ?? "",
            };

            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken, adminKey);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var payload = await resp.Content.ReadFromJsonAsync<PublishActionResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            return payload?.News is not null ? ToModel(payload.News) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> UploadCoverImageAsync(
        byte[] imageBytes,
        string sessionToken,
        string? adminKey,
        CancellationToken ct = default)
    {
        try
        {
            if (imageBytes is not { Length: > 0 } || imageBytes.Length > 3_200_000 || string.IsNullOrWhiteSpace(sessionToken))
                return null;

            var body = new
            {
                action = "upload_cover",
                imageBase64 = Convert.ToBase64String(imageBytes),
                adminKey = adminKey?.Trim() ?? "",
            };

            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken, adminKey);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var payload = await resp.Content.ReadFromJsonAsync<UploadCoverResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            return string.IsNullOrWhiteSpace(payload?.CoverImageUrl) ? null : payload.CoverImageUrl;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeleteNewsAsync(
        string newsId,
        string sessionToken,
        string? adminKey,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newsId) || string.IsNullOrWhiteSpace(sessionToken))
                return false;

            var body = new
            {
                action = "delete_news",
                newsId = newsId.Trim(),
                adminKey = adminKey?.Trim() ?? "",
            };

            using var req = CreateRequest(HttpMethod.Post, Endpoint, sessionToken, adminKey);
            req.Content = JsonContent.Create(body, options: JsonOpts);

            using var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static NewsArticle ToModel(NewsArticleDto dto) => new()
    {
        Id = dto.Id ?? "",
        Title = dto.Title ?? "",
        Content = dto.Content ?? "",
        Category = dto.Category ?? "UPDATE",
        IsPinned = dto.IsPinned,
        AuthorId = dto.AuthorId ?? "",
        AuthorName = string.IsNullOrWhiteSpace(dto.AuthorName) ? "IL DENTE PROIBITO" : dto.AuthorName,
        AuthorAvatar = dto.AuthorAvatar ?? "",
        CoverImageUrl = dto.CoverImageUrl ?? "",
        CreatedAt = DateTimeOffset.TryParse(dto.CreatedAt, out var dt) ? dt : DateTimeOffset.UtcNow,
        Reactions = dto.Reactions ?? new Dictionary<string, int>(),
        MyReactions = dto.MyReactions ?? new List<string>(),
        CommentsCount = dto.CommentsCount,
    };

    private static NewsComment ToCommentModel(NewsCommentDto dto) => new()
    {
        Id = dto.Id ?? "",
        NewsId = dto.NewsId ?? "",
        DiscordId = dto.DiscordId ?? "",
        Username = dto.Username ?? "User",
        DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? (dto.Username ?? "User") : dto.DisplayName,
        AvatarUrl = dto.AvatarUrl ?? "",
        Badges = dto.Badges ?? new List<string>(),
        Content = dto.Content ?? "",
        CreatedAt = DateTimeOffset.TryParse(dto.CreatedAt, out var dt) ? dt : DateTimeOffset.UtcNow,
        CanDelete = dto.CanDelete,
    };

    private sealed class NewsFeedResponseDto
    {
        public List<NewsArticleDto>? News { get; set; }
    }

    private sealed class NewsDetailsResponseDto
    {
        public NewsArticleDto? News { get; set; }
        public List<NewsCommentDto>? Comments { get; set; }
    }

    private sealed class PublishActionResponseDto
    {
        public bool Success { get; set; }
        public NewsArticleDto? News { get; set; }
    }

    private sealed class UploadCoverResponseDto
    {
        public bool Ok { get; set; }
        public string? CoverImageUrl { get; set; }
    }

    private sealed class CommentActionResponseDto
    {
        public bool Success { get; set; }
        public NewsCommentDto? Comment { get; set; }
    }

    private sealed class NewsArticleDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? Category { get; set; }
        public bool IsPinned { get; set; }
        public string? AuthorId { get; set; }
        public string? AuthorName { get; set; }
        public string? AuthorAvatar { get; set; }
        public string? CoverImageUrl { get; set; }
        public string? CreatedAt { get; set; }
        public Dictionary<string, int>? Reactions { get; set; }
        public List<string>? MyReactions { get; set; }
        public int CommentsCount { get; set; }
    }

    private sealed class NewsCommentDto
    {
        public string? Id { get; set; }
        public string? NewsId { get; set; }
        public string? DiscordId { get; set; }
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public List<string>? Badges { get; set; }
        public string? Content { get; set; }
        public string? CreatedAt { get; set; }
        public bool CanDelete { get; set; }
    }
}
