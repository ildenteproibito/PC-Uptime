using System.Net.Http.Json;
using System.Text.Json;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class CommunityFixesService : ICommunityFixesService
{
    private const string Endpoint = "https://uptime-pc.vercel.app/api/fixes";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<CommunityFixItem>> FetchFixesAsync(
        int? eventId = null,
        string? provider = null,
        string? query = null,
        CancellationToken ct = default)
    {
        try
        {
            var qs = new List<string>();
            if (eventId.HasValue) qs.Add($"eventId={eventId.Value}");
            if (!string.IsNullOrWhiteSpace(provider)) qs.Add($"provider={Uri.EscapeDataString(provider.Trim())}");
            if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query.Trim())}");
            var url = qs.Count > 0 ? $"{Endpoint}?{string.Join("&", qs)}" : Endpoint;

            using var resp = await Http.GetAsync(url, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return Array.Empty<CommunityFixItem>();

            var payload = await resp.Content.ReadFromJsonAsync<FixesResponseDto>(JsonOpts, ct).ConfigureAwait(false);
            if (payload?.fixes is null) return Array.Empty<CommunityFixItem>();

            return payload.fixes
                .Where(f => !string.IsNullOrWhiteSpace(f.id) && !string.IsNullOrWhiteSpace(f.title))
                .Select(ToModel)
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return Array.Empty<CommunityFixItem>();
        }
    }

    public async Task<CommunityFixResult> SubmitFixAsync(
        int? eventId,
        string provider,
        string title,
        string steps,
        string author,
        CancellationToken ct = default)
    {
        try
        {
            var req = new SubmitFixDto(eventId, provider.Trim(), title.Trim(), steps.Trim(), string.IsNullOrWhiteSpace(author) ? "Community Member" : author.Trim());
            using var resp = await Http.PostAsJsonAsync(Endpoint, req, JsonOpts, ct).ConfigureAwait(false);
            ActionResponseDto? body = null;
            try { body = await resp.Content.ReadFromJsonAsync<ActionResponseDto>(JsonOpts, ct).ConfigureAwait(false); }
            catch (JsonException) { /* non-JSON error body */ }
            if (!resp.IsSuccessStatusCode)
            {
                return new CommunityFixResult(false, body?.error ?? $"HTTP {(int)resp.StatusCode}");
            }
            return new CommunityFixResult(true, "Published", body?.fix is not null ? ToModel(body.fix) : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new CommunityFixResult(false, ex.Message);
        }
    }

    public async Task<CommunityFixResult> UpvoteFixAsync(string fixId, CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.PostAsJsonAsync($"{Endpoint}?action=upvote", new IdPayloadDto(fixId), JsonOpts, ct).ConfigureAwait(false);
            ActionResponseDto? body = null;
            try { body = await resp.Content.ReadFromJsonAsync<ActionResponseDto>(JsonOpts, ct).ConfigureAwait(false); }
            catch (JsonException) { /* non-JSON error body */ }
            if (!resp.IsSuccessStatusCode)
            {
                return new CommunityFixResult(false, body?.error ?? $"HTTP {(int)resp.StatusCode}");
            }
            return new CommunityFixResult(true, "Voted", Upvotes: body?.upvotes, AlreadyVoted: body?.alreadyVoted == true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new CommunityFixResult(false, ex.Message);
        }
    }

    public async Task<CommunityFixResult> ReportFixAsync(string fixId, string reason = "", CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.PostAsJsonAsync($"{Endpoint}?action=report", new ReportPayloadDto(fixId, reason), JsonOpts, ct).ConfigureAwait(false);
            return new CommunityFixResult(resp.IsSuccessStatusCode, resp.IsSuccessStatusCode ? "Reported" : $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new CommunityFixResult(false, ex.Message);
        }
    }

    private static CommunityFixItem ToModel(FixDto f) => new(
        f.id ?? "",
        f.event_id,
        f.provider ?? "",
        f.title ?? "",
        f.steps ?? "",
        f.author ?? "Community Member",
        f.author_role ?? "member",
        f.upvotes,
        f.created_at ?? "");

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record FixesResponseDto(List<FixDto>? fixes);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record FixDto(
        string? id,
        int? event_id,
        string? provider,
        string? title,
        string? steps,
        string? author,
        string? author_role,
        int upvotes,
        string? created_at);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record SubmitFixDto(int? eventId, string provider, string title, string steps, string author);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record IdPayloadDto(string id);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record ReportPayloadDto(string id, string reason);

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed record ActionResponseDto(bool ok, string? error, FixDto? fix, int? upvotes, bool alreadyVoted);
}
