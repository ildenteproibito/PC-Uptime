namespace Uptime.Core;

public sealed class ConnectedAccount
{
    public string Type { get; init; } = "";
    public string Platform { get; init; } = "";
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public bool Verified { get; init; }
    public bool Hidden { get; init; }
}

public sealed class FriendProfileSummary
{
    public string DiscordId { get; init; } = "";
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public string CustomStatus { get; init; } = "";
    public List<string> Badges { get; init; } = new();
}

public sealed class UserProfileData
{
    public string DiscordId { get; init; } = "";
    public string Username { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string AvatarUrl { get; init; } = "";
    public string BannerUrl { get; init; } = "";
    public string BannerColor { get; init; } = "";
    public string CustomStatus { get; init; } = "";
    public string Bio { get; init; } = "";
    public List<string> Badges { get; init; } = new();
    public List<ConnectedAccount> Connections { get; init; } = new();
    public List<string> HiddenConnections { get; init; } = new();
    public List<string> FavoriteWallpaperIds { get; init; } = new();
    public List<FriendProfileSummary> Friends { get; init; } = new();
    public List<FriendProfileSummary> IncomingRequests { get; init; } = new();
    public List<FriendProfileSummary> OutgoingRequests { get; init; } = new();
    public List<FriendProfileSummary> DiscoverUsers { get; init; } = new();
}

public sealed class FriendChatMessage
{
    public string Id { get; init; } = "";
    public string FromDiscordId { get; init; } = "";
    public string Text { get; init; } = "";
    public DateTimeOffset Timestamp { get; init; }
}

public interface IAccountAuthService
{
    bool HasSavedSession { get; }
    string? GetSessionToken();
    Task<UserProfileData?> RestoreSessionAsync(CancellationToken cancellationToken = default);
    Task<UserProfileData> LoginWithDiscordAsync(CancellationToken cancellationToken = default);
    Task<UserProfileData> UpdateProfileAsync(string? bio, IReadOnlyList<string>? hiddenConnections, CancellationToken cancellationToken = default);
    Task<UserProfileData> UpdateCustomStatusAsync(string? customStatus, CancellationToken cancellationToken = default);
    Task<UserProfileData> UpdateFavoritesAsync(IReadOnlyList<string> favoriteWallpaperIds, CancellationToken cancellationToken = default);
    Task<UserProfileData> AddFriendAsync(string query, CancellationToken cancellationToken = default);
    Task<UserProfileData> AcceptFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default);
    Task<UserProfileData> DeclineFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default);
    Task<UserProfileData> RemoveFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendChatMessage>> GetFriendChatAsync(string friendDiscordId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FriendChatMessage>> SendFriendMessageAsync(string friendDiscordId, string text, CancellationToken cancellationToken = default);
    Task<UserProfileData?> GetPublicProfileAsync(string authorName, CancellationToken cancellationToken = default);
    Task DeleteAccountAsync(CancellationToken cancellationToken = default);
    void Logout();
}
