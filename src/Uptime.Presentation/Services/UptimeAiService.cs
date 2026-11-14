using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Uptime.Presentation.Services;

public sealed record AiChatTurn(string role, string content);

public sealed record AiSystemContext(
    string appVersion,
    string uptime,
    string health,
    string os,
    int cpuThreads,
    long idleSeconds,
    string powerPlan,
    bool keepAwake,
    bool hasBattery,
    int batteryPercent,
    bool isCharging);

public sealed record AiAssistantResult(bool Success, string ReplyOrError, int StatusCode = 200);

public static class UptimeAiService
{
    public const string EndpointUrl = "https://uptime-pc.vercel.app/api/assistant";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<AiAssistantResult> AskAsync(
        string message,
        IReadOnlyList<AiChatTurn> history,
        string language,
        AiSystemContext? context,
        CancellationToken ct = default)
    {
        var historyArray = new JsonArray();
        foreach (var turn in history)
        {
            historyArray.Add(new JsonObject
            {
                ["role"] = turn.role,
                ["content"] = turn.content
            });
        }

        var root = new JsonObject
        {
            ["message"] = message,
            ["history"] = historyArray,
            ["language"] = language
        };

        if (context is not null)
        {
            root["context"] = new JsonObject
            {
                ["appVersion"] = context.appVersion,
                ["uptime"] = context.uptime,
                ["health"] = context.health,
                ["os"] = context.os,
                ["cpuThreads"] = context.cpuThreads,
                ["idleSeconds"] = context.idleSeconds,
                ["powerPlan"] = context.powerPlan,
                ["keepAwake"] = context.keepAwake,
                ["hasBattery"] = context.hasBattery,
                ["batteryPercent"] = context.batteryPercent,
                ["isCharging"] = context.isCharging
            };
        }

        using var content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await Http.PostAsync(EndpointUrl, content, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);

        string? reply = null;
        string? error = null;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("reply", out var replyProp))
                reply = replyProp.GetString();
            if (doc.RootElement.TryGetProperty("error", out var errProp))
                error = errProp.GetString();
        }
        catch (JsonException)
        {
            // Fall back to status-based error handling below.
        }

        if (response.IsSuccessStatusCode && !string.IsNullOrWhiteSpace(reply))
        {
            return new AiAssistantResult(true, reply.Trim(), (int)response.StatusCode);
        }

        var fallback = string.IsNullOrWhiteSpace(error)
            ? $"HTTP {(int)response.StatusCode} ({response.StatusCode})"
            : error;

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
            return new AiAssistantResult(false, fallback, 429);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return new AiAssistantResult(false, fallback, 503);

        return new AiAssistantResult(false, fallback, (int)response.StatusCode);
    }
}
