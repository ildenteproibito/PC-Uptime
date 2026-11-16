using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class DiscordAuthService : IAccountAuthService
{
    private const string DiscordClientId = "1570928729733988382";
    private const string RedirectUri = "https://uptime-pc.vercel.app/api/auth-callback";
    private const string AuthApiUrl = "https://uptime-pc.vercel.app/api/auth";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static string SessionPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "session.dat");

    public bool HasSavedSession => File.Exists(SessionPath);
    public string? GetSessionToken() => LoadProtectedToken();

    private static string? LoadProtectedToken()
    {
        try
        {
            if (!File.Exists(SessionPath)) return null;
            var encrypted = File.ReadAllBytes(SessionPath);
            var raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var token = Encoding.UTF8.GetString(raw).Trim();
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    private static void SaveProtectedToken(string token)
    {
        var dir = Path.GetDirectoryName(SessionPath);
        if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
        var raw = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
        var tmp = SessionPath + ".tmp";
        File.WriteAllBytes(tmp, encrypted);
        File.Move(tmp, SessionPath, overwrite: true);
    }

    public void Logout()
    {
        try
        {
            if (File.Exists(SessionPath)) File.Delete(SessionPath);
        }
        catch
        {
            // Ignore file delete error
        }
    }

    public async Task<UserProfileData?> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken();
        if (string.IsNullOrWhiteSpace(token)) return null;

        using var req = new HttpRequestMessage(HttpMethod.Get, AuthApiUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await Http.SendAsync(req, cancellationToken);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
        {
            Logout();
            return null;
        }
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, _, profile) = ParseAuthResponse(json);
        return ok ? profile : null;
    }

    public async Task<UserProfileData> LoginWithDiscordAsync(CancellationToken cancellationToken = default)
    {
        var port = GetFreeLoopbackPort();
        var verifier = Base64UrlRandom(48);
        var challenge = Base64UrlSha256(verifier);
        var nonce = Base64UrlRandom(16);
        var state = $"p{port}_{nonce}";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/callback/");
        listener.Start();

        var authorizeUrl =
            $"https://discord.com/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(DiscordClientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&scope={Uri.EscapeDataString("identify connections")}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            $"&code_challenge_method=S256" +
            $"&prompt=consent";

        using var proc = Process.Start(new ProcessStartInfo { FileName = authorizeUrl, UseShellExecute = true });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(3));

        HttpListenerContext ctx;
        while (true)
        {
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Discord login timed out. Please try again.");
            }

            if (ctx.Request.Url?.AbsolutePath.StartsWith("/callback", StringComparison.OrdinalIgnoreCase) == true)
                break;

            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }

        var code = ctx.Request.QueryString["code"];
        var returnedState = ctx.Request.QueryString["state"];
        var okHtml = "<!doctype html><html><body style='background:#0b0f15;color:#7bc4a8;font-family:system-ui;display:grid;place-items:center;height:100vh;margin:0'><h3>Login completato! Puoi chiudere questa scheda e tornare su Uptime.</h3><script>setTimeout(()=>window.close(),1200)</script></body></html>";
        var errHtml = "<!doctype html><html><body style='background:#0b0f15;color:#f87171;font-family:system-ui;display:grid;place-items:center;height:100vh;margin:0'><h3>Verifica OAuth fallita. Riprova da Uptime.</h3></body></html>";

        var validCallback = !string.IsNullOrWhiteSpace(code) && string.Equals(returnedState, state, StringComparison.Ordinal);
        var buffer = Encoding.UTF8.GetBytes(validCallback ? okHtml : errHtml);
        ctx.Response.ContentType = "text/html; charset=utf-8";
        ctx.Response.ContentLength64 = buffer.Length;
        await ctx.Response.OutputStream.WriteAsync(buffer, cancellationToken);
        ctx.Response.Close();
        listener.Stop();

        if (!validCallback)
            throw new InvalidOperationException("OAuth state mismatch or missing authorization code.");

        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = "exchange",
            ["code"] = code,
            ["codeVerifier"] = verifier,
            ["redirectUri"] = RedirectUri
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, token, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || string.IsNullOrWhiteSpace(token) || profile is null)
            throw new InvalidOperationException(error ?? $"Auth failed ({(int)resp.StatusCode})");

        SaveProtectedToken(token);
        return profile;
    }

    public async Task<UserProfileData> UpdateProfileAsync(
        string? bio,
        IReadOnlyList<string>? hiddenConnections,
        CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var dict = new Dictionary<string, object?> { ["action"] = "update-profile" };
        if (bio is not null) dict["bio"] = bio;
        if (hiddenConnections is not null) dict["hiddenConnections"] = hiddenConnections;
        var payload = JsonSerializer.Serialize(dict);

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || profile is null)
            throw new InvalidOperationException(error ?? "Failed to update profile.");

        return profile;
    }

    public async Task<UserProfileData> UpdateCustomStatusAsync(
        string? customStatus,
        CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = "update-profile",
            ["customStatus"] = customStatus ?? ""
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || profile is null)
            throw new InvalidOperationException(error ?? "Failed to update status.");

        return profile;
    }

    public async Task<UserProfileData> UpdateFavoritesAsync(
        IReadOnlyList<string> favoriteWallpaperIds,
        CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = "update-profile",
            ["preferences"] = new Dictionary<string, object?>
            {
                ["favorites"] = favoriteWallpaperIds
            }
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || profile is null)
            throw new InvalidOperationException(error ?? "Failed to save favorites.");

        return profile;
    }

    public async Task<UserProfileData> AddFriendAsync(string query, CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = "add-friend",
            ["query"] = (query ?? "").Trim()
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || profile is null)
            throw new InvalidOperationException(error ?? "Could not add friend.");

        return profile;
    }

    public Task<UserProfileData> AcceptFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default)
        => SendFriendActionAsync("accept-friend", friendDiscordId, "Could not accept friend request.", cancellationToken);

    public Task<UserProfileData> DeclineFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default)
        => SendFriendActionAsync("decline-friend", friendDiscordId, "Could not decline friend request.", cancellationToken);

    public Task<UserProfileData> RemoveFriendAsync(string friendDiscordId, CancellationToken cancellationToken = default)
        => SendFriendActionAsync("remove-friend", friendDiscordId, "Could not remove friend.", cancellationToken);

    private async Task<UserProfileData> SendFriendActionAsync(string action, string friendDiscordId, string fallbackError, CancellationToken cancellationToken)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = action,
            ["friendDiscordId"] = (friendDiscordId ?? "").Trim()
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        var (ok, _, error, profile) = ParseAuthResponse(respJson);

        if (!resp.IsSuccessStatusCode || !ok || profile is null)
            throw new InvalidOperationException(error ?? fallbackError);

        return profile;
    }

    public async Task<IReadOnlyList<FriendChatMessage>> GetFriendChatAsync(string friendDiscordId, CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        if (string.IsNullOrWhiteSpace(friendDiscordId)) return Array.Empty<FriendChatMessage>();

        var url = $"{AuthApiUrl}?chatWith={Uri.EscapeDataString(friendDiscordId.Trim())}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        return ParseChatMessages(json);
    }

    public async Task<IReadOnlyList<FriendChatMessage>> SendFriendMessageAsync(string friendDiscordId, string text, CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = "send-message",
            ["friendDiscordId"] = (friendDiscordId ?? "").Trim(),
            ["text"] = (text ?? "").Trim()
        });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            var (_, _, error, _) = ParseAuthResponse(json);
            throw new InvalidOperationException(error ?? "Could not send message.");
        }
        return ParseChatMessages(json);
    }

    private static IReadOnlyList<FriendChatMessage> ParseChatMessages(string json)
    {
        var list = new List<FriendChatMessage>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("messages", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return list;

            foreach (var m in arr.EnumerateArray())
            {
                var tsStr = m.TryGetProperty("ts", out var tsEl) ? tsEl.GetString() : null;
                DateTimeOffset.TryParse(tsStr, out var ts);
                list.Add(new FriendChatMessage
                {
                    Id = m.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                    FromDiscordId = m.TryGetProperty("from", out var frEl) ? frEl.GetString() ?? "" : "",
                    Text = m.TryGetProperty("text", out var txEl) ? txEl.GetString() ?? "" : "",
                    Timestamp = ts
                });
            }
        }
        catch { /* ignore */ }
        return list;
    }

    public async Task<UserProfileData?> GetPublicProfileAsync(string authorName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorName)) return null;
        try
        {
            var url = $"{AuthApiUrl}?author={Uri.EscapeDataString(authorName.Trim())}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await Http.SendAsync(req, cancellationToken);
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync(cancellationToken);
            var (ok, _, _, profile) = ParseAuthResponse(json);
            return ok ? profile : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task DeleteAccountAsync(CancellationToken cancellationToken = default)
    {
        var token = LoadProtectedToken() ?? throw new InvalidOperationException("Not signed in.");
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?> { ["action"] = "delete-account" });

        using var req = new HttpRequestMessage(HttpMethod.Post, AuthApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to delete account ({(int)resp.StatusCode}).");

        Logout();
    }

    private static int GetFreeLoopbackPort()
    {
        using var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        return ((IPEndPoint)tcp.LocalEndpoint).Port;
    }

    private static string Base64UrlRandom(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Base64UrlSha256(string input)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(input));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static (bool Ok, string? Token, string? Error, UserProfileData? Profile) ParseAuthResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            var token = root.TryGetProperty("token", out var tokEl) ? tokEl.GetString() : null;
            var error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() : null;

            UserProfileData? profile = null;
            if (root.TryGetProperty("profile", out var pEl) && pEl.ValueKind == JsonValueKind.Object)
            {
                var badges = new List<string>();
                if (pEl.TryGetProperty("badges", out var bArr) && bArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var b in bArr.EnumerateArray())
                    {
                        var bs = b.GetString();
                        if (!string.IsNullOrWhiteSpace(bs)) badges.Add(bs);
                    }
                }

                var connections = new List<ConnectedAccount>();
                if (pEl.TryGetProperty("connections", out var cArr) && cArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cArr.EnumerateArray())
                    {
                        if (c.ValueKind != JsonValueKind.Object) continue;
                        connections.Add(new ConnectedAccount
                        {
                            Type = c.TryGetProperty("type", out var tEl) ? tEl.GetString() ?? "" : "",
                            Platform = c.TryGetProperty("platform", out var plEl) ? plEl.GetString() ?? "" : "",
                            Id = c.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "",
                            Name = c.TryGetProperty("name", out var nEl) ? nEl.GetString() ?? "" : "",
                            Url = c.TryGetProperty("url", out var uEl) ? uEl.GetString() ?? "" : "",
                            Verified = c.TryGetProperty("verified", out var vEl) && vEl.ValueKind == JsonValueKind.True,
                            Hidden = c.TryGetProperty("hidden", out var hEl) && hEl.ValueKind == JsonValueKind.True
                        });
                    }
                }

                var hidden = new List<string>();
                if (pEl.TryGetProperty("hiddenConnections", out var hcArr) && hcArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var h in hcArr.EnumerateArray())
                    {
                        var hs = h.GetString();
                        if (!string.IsNullOrWhiteSpace(hs)) hidden.Add(hs);
                    }
                }

                var favorites = new List<string>();
                if (pEl.TryGetProperty("preferences", out var prefEl) && prefEl.ValueKind == JsonValueKind.Object &&
                    prefEl.TryGetProperty("favorites", out var favArr) && favArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in favArr.EnumerateArray())
                    {
                        var fs = f.GetString();
                        if (!string.IsNullOrWhiteSpace(fs)) favorites.Add(fs);
                    }
                }

                var friends = ParseFriendSummaryList(pEl, "friends");
                var incomingRequests = ParseFriendSummaryList(pEl, "incomingRequests");
                var outgoingRequests = ParseFriendSummaryList(pEl, "outgoingRequests");
                var discoverUsers = ParseFriendSummaryList(pEl, "discoverUsers");

                profile = new UserProfileData
                {
                    DiscordId = pEl.TryGetProperty("discordId", out var dIdEl) ? dIdEl.GetString() ?? "" : "",
                    Username = pEl.TryGetProperty("username", out var unEl) ? unEl.GetString() ?? "" : "",
                    DisplayName = pEl.TryGetProperty("displayName", out var dnEl) ? dnEl.GetString() ?? "" : "",
                    AvatarUrl = pEl.TryGetProperty("avatarUrl", out var avEl) ? avEl.GetString() ?? "" : "",
                    BannerUrl = pEl.TryGetProperty("bannerUrl", out var bnEl) ? bnEl.GetString() ?? "" : "",
                    BannerColor = pEl.TryGetProperty("bannerColor", out var bcEl) ? bcEl.GetString() ?? "" : "",
                    CustomStatus = pEl.TryGetProperty("customStatus", out var csEl) ? csEl.GetString() ?? "" : "",
                    Bio = pEl.TryGetProperty("bio", out var bioEl) ? bioEl.GetString() ?? "" : "",
                    Badges = badges,
                    Connections = connections,
                    HiddenConnections = hidden,
                    FavoriteWallpaperIds = favorites,
                    Friends = friends,
                    IncomingRequests = incomingRequests,
                    OutgoingRequests = outgoingRequests,
                    DiscoverUsers = discoverUsers
                };
            }

            return (ok, token, error, profile);
        }
        catch
        {
            return (false, null, "Invalid response from auth server.", null);
        }
    }

    private static List<FriendProfileSummary> ParseFriendSummaryList(JsonElement pEl, string propertyName)
    {
        var list = new List<FriendProfileSummary>();
        if (!pEl.TryGetProperty(propertyName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var fr in arr.EnumerateArray())
        {
            var frBadges = new List<string>();
            if (fr.TryGetProperty("badges", out var fbEl) && fbEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var b in fbEl.EnumerateArray())
                {
                    var bs = b.GetString();
                    if (!string.IsNullOrWhiteSpace(bs)) frBadges.Add(bs);
                }
            }
            list.Add(new FriendProfileSummary
            {
                DiscordId = fr.TryGetProperty("discordId", out var fId) ? fId.GetString() ?? "" : "",
                Username = fr.TryGetProperty("username", out var fUn) ? fUn.GetString() ?? "" : "",
                DisplayName = fr.TryGetProperty("displayName", out var fDn) ? fDn.GetString() ?? "" : "",
                AvatarUrl = fr.TryGetProperty("avatarUrl", out var fAv) ? fAv.GetString() ?? "" : "",
                CustomStatus = fr.TryGetProperty("customStatus", out var fCs) ? fCs.GetString() ?? "" : "",
                Badges = frBadges
            });
        }

        return list;
    }
}
