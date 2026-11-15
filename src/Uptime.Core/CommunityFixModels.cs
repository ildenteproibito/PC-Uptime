namespace Uptime.Core;

public sealed record CommunityFixItem(
    string Id,
    int? EventId,
    string Provider,
    string Title,
    string Steps,
    string Author,
    string AuthorRole,
    int Upvotes,
    string CreatedAt);

public sealed record CommunityFixResult(bool Ok, string Message, CommunityFixItem? CreatedFix = null, int? Upvotes = null, bool AlreadyVoted = false);

public interface ICommunityFixesService
{
    Task<IReadOnlyList<CommunityFixItem>> FetchFixesAsync(int? eventId = null, string? provider = null, string? query = null, CancellationToken ct = default);
    Task<CommunityFixResult> SubmitFixAsync(int? eventId, string provider, string title, string steps, string author, CancellationToken ct = default);
    Task<CommunityFixResult> UpvoteFixAsync(string fixId, CancellationToken ct = default);
    Task<CommunityFixResult> ReportFixAsync(string fixId, string reason = "", CancellationToken ct = default);
}
