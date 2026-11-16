namespace Uptime.Core;

public sealed class NewsArticle
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Content { get; init; } = "";
    public string Category { get; init; } = "UPDATE";
    public bool IsPinned { get; init; }
    public string AuthorId { get; init; } = "";
    public string AuthorName { get; init; } = "IL DENTE PROIBITO";
    public string AuthorAvatar { get; init; } = "";
    public string CoverImageUrl { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, int> Reactions { get; init; } = new();
    public List<string> MyReactions { get; init; } = new();
    public int CommentsCount { get; init; }
}

public sealed class NewsComment
{
    public string Id { get; init; } = "";
    public string NewsId { get; init; } = "";
    public string DiscordId { get; init; } = "";
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public List<string> Badges { get; init; } = new();
    public string Content { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool CanDelete { get; init; }
}

public interface INewsService
{
    Task<IReadOnlyList<NewsArticle>> GetNewsFeedAsync(string? sessionToken = null, CancellationToken ct = default);
    Task<(NewsArticle? Article, IReadOnlyList<NewsComment> Comments)> GetNewsDetailsAsync(string newsId, string? sessionToken = null, CancellationToken ct = default);
    Task<bool> ToggleReactionAsync(string newsId, string emoji, string sessionToken, CancellationToken ct = default);
    Task<NewsComment?> AddCommentAsync(string newsId, string content, string sessionToken, CancellationToken ct = default);
    Task<bool> DeleteCommentAsync(string commentId, string sessionToken, CancellationToken ct = default);
    Task<NewsArticle?> PublishNewsAsync(string title, string content, string category, bool isPinned, string? coverImageUrl, string sessionToken, string? adminKey, CancellationToken ct = default);
    Task<string?> UploadCoverImageAsync(byte[] imageBytes, string sessionToken, string? adminKey, CancellationToken ct = default);
    Task<bool> DeleteNewsAsync(string newsId, string sessionToken, string? adminKey, CancellationToken ct = default);
}
