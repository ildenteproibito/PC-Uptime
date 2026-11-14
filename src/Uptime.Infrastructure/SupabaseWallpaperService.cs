using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Uptime.Core;

namespace Uptime.Infrastructure;

public sealed class SupabaseWallpaperService : ICommunityWallpaperService
{
    public const string DefaultApiEndpoint = "https://uptime-pc.vercel.app/api/wallpapers";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string ResolveEndpoint()
    {
        try
        {
            var cfgPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Uptime",
                "supabase.json");
            if (File.Exists(cfgPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(cfgPath));
                if (doc.RootElement.TryGetProperty("apiUrl", out var a) && a.GetString() is { Length: > 0 } aStr)
                    return aStr.Trim().TrimEnd('/');
            }
        }
        catch
        {
            // Ignore malformed optional override file
        }

        return DefaultApiEndpoint;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveEndpoint());

    public async Task<IReadOnlyList<CommunityWallpaperItem>> GetWallpapersAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveEndpoint();
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}?offset=0&limit=500");
        using var resp = await Http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<ApiListResponseDto>(json, JsonOptions);
        var rows = payload?.Items ?? new List<WallpaperRowDto>();
        return rows.Select(ToItem).ToList();
    }

    public async Task<CommunityWallpaperItem> UploadWallpaperAsync(
        string filePath,
        string title,
        string author,
        string resolution,
        string themeTag,
        CancellationToken cancellationToken = default)
    {
        var endpoint = ResolveEndpoint();
        var info = new FileInfo(filePath);
        if (!info.Exists)
            throw new FileNotFoundException("Wallpaper file not found.", filePath);
        const long maxUploadBytes = 50L * 1024 * 1024;
        if (info.Length > maxUploadBytes)
            throw new InvalidOperationException("Wallpaper exceeds 50 MB desktop upload limit.");

        var ext = info.Extension.ToLowerInvariant();
        var (mime, cleanExt) = ext switch
        {
            ".jpg" or ".jpeg" => ("image/jpeg", "jpg"),
            ".webp" => ("image/webp", "webp"),
            ".bmp" => ("image/bmp", "bmp"),
            ".avif" => ("image/avif", "avif"),
            ".png" => ("image/png", "png"),
            _ => throw new InvalidOperationException("Unsupported image format. Use PNG, JPG, WEBP, BMP, or AVIF.")
        };

        var fallbackTitle = Path.GetFileNameWithoutExtension(filePath);
        if (string.IsNullOrWhiteSpace(fallbackTitle)) fallbackTitle = "Wallpaper";
        var effectiveTitle = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim();
        var effectiveAuthor = string.IsNullOrWhiteSpace(author) ? "Anonymous" : author.Trim();

        if (effectiveTitle.Length < 3)
            throw new InvalidOperationException("Title must be at least 3 characters.");
        if (ContainsBlockedTerms(effectiveTitle) || ContainsBlockedTerms(effectiveAuthor) || ContainsBlockedTerms(info.Name))
            throw new InvalidOperationException("NSFW, gore, or explicit terms are not allowed.");

        var safeTitle = effectiveTitle[..Math.Min(effectiveTitle.Length, 80)];
        var safeAuthor = effectiveAuthor[..Math.Min(effectiveAuthor.Length, 40)];
        var safeResolution = string.IsNullOrWhiteSpace(resolution) ? "1920x1080" : resolution;
        var safeThemeTag = string.IsNullOrWhiteSpace(themeTag) ? "Custom" : themeTag;

        string respJson;
        if (info.Length <= 3_100_000)
        {
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            respJson = await PostApiJsonAsync(endpoint, new Dictionary<string, object?>
            {
                ["action"] = "publish",
                ["title"] = safeTitle,
                ["author"] = safeAuthor,
                ["resolution"] = safeResolution,
                ["themeTag"] = safeThemeTag,
                ["mimeType"] = mime,
                ["fileExt"] = cleanExt,
                ["base64"] = Convert.ToBase64String(bytes)
            }, cancellationToken);
        }
        else
        {
            var prepJson = await PostApiJsonAsync(endpoint, new Dictionary<string, object?>
            {
                ["action"] = "prepare-upload",
                ["title"] = safeTitle,
                ["author"] = safeAuthor,
                ["fileExt"] = cleanExt,
                ["fileSize"] = info.Length
            }, cancellationToken);

            using var prepDoc = JsonDocument.Parse(prepJson);
            var uploadUrl = prepDoc.RootElement.GetProperty("uploadUrl").GetString()
                ?? throw new InvalidOperationException("Failed to initialize direct upload.");
            var storagePath = prepDoc.RootElement.GetProperty("storagePath").GetString()
                ?? throw new InvalidOperationException("Missing storage path.");

            await using (var fs = File.OpenRead(filePath))
            using (var putContent = new StreamContent(fs))
            {
                putContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
                using var putReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl) { Content = putContent };
                using var putResp = await Http.SendAsync(putReq, cancellationToken);
                if (!putResp.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException($"Direct storage upload failed (HTTP {(int)putResp.StatusCode}).");
                }
            }

            respJson = await PostApiJsonAsync(endpoint, new Dictionary<string, object?>
            {
                ["action"] = "finalize-upload",
                ["storagePath"] = storagePath,
                ["title"] = safeTitle,
                ["author"] = safeAuthor,
                ["resolution"] = safeResolution,
                ["themeTag"] = safeThemeTag
            }, cancellationToken);
        }

        var result = JsonSerializer.Deserialize<ApiPublishResponseDto>(respJson, JsonOptions);
        var created = result?.Item ?? throw new InvalidOperationException("Unexpected response from server.");
        if (!string.IsNullOrWhiteSpace(created.Id) && !string.IsNullOrWhiteSpace(result.Token))
        {
            SaveDeleteToken(created.Id, result.Token);
        }

        return ToItem(created);
    }

    private static async Task<string> PostApiJsonAsync(string endpoint, Dictionary<string, object?> body, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var resp = await Http.SendAsync(req, cancellationToken);
        var respJson = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                using var errDoc = JsonDocument.Parse(respJson);
                if (errDoc.RootElement.TryGetProperty("error", out var errProp) && errProp.GetString() is { Length: > 0 } errMsg)
                    throw new InvalidOperationException(errMsg);
            }
            catch (JsonException) { }
            throw new InvalidOperationException($"Upload failed (HTTP {(int)resp.StatusCode}).");
        }
        return respJson;
    }

    public async Task<string> DownloadWallpaperToTempAsync(CommunityWallpaperItem item, bool countDownload = true, CancellationToken cancellationToken = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "UptimeCommunityWallpapers");
        Directory.CreateDirectory(tempDir);

        var ext = Path.GetExtension(new Uri(item.ImageUrl).AbsolutePath);
        if (string.IsNullOrWhiteSpace(ext)) ext = ".png";

        var safeId = string.Concat(item.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(safeId)) safeId = "wallpaper";
        var localPath = Path.Combine(tempDir, $"{safeId}{ext}");
        var isCached = File.Exists(localPath) && new FileInfo(localPath).Length > 0;
        if (!isCached)
        {
            var bytes = await Http.GetByteArrayAsync(item.ImageUrl, cancellationToken);
            var tmpPath = $"{localPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(tmpPath, bytes, cancellationToken);
                File.Move(tmpPath, localPath, true);
            }
            finally
            {
                if (File.Exists(tmpPath)) try { File.Delete(tmpPath); } catch { }
            }
        }

        if (countDownload)
        {
            var itemId = item.Id;
            _ = Task.Run(async () =>
            {
                try
                {
                    var endpoint = ResolveEndpoint();
                    var payload = JsonSerializer.Serialize(new Dictionary<string, string>
                    {
                        ["action"] = "download",
                        ["id"] = itemId
                    });
                    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                    };
                    using var _ = await Http.SendAsync(req);
                }
                catch
                {
                    // Non-fatal if download counter increment fails
                }
            });
        }

        return localPath;
    }

    public async Task<bool> DeleteWallpaperAsync(string wallpaperId, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(wallpaperId) || string.IsNullOrWhiteSpace(token))
            return false;

        var endpoint = ResolveEndpoint();
        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["action"] = "delete",
            ["id"] = wallpaperId,
            ["token"] = token
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        using var resp = await Http.SendAsync(req, cancellationToken);
        if (!resp.IsSuccessStatusCode) return false;

        RemoveDeleteToken(wallpaperId);
        return true;
    }

    private static string TokensFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Uptime",
        "wallpaper_tokens.json");

    public static Dictionary<string, string> GetSavedDeleteTokens()
    {
        try
        {
            if (!File.Exists(TokensFilePath)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(TokensFilePath)) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static readonly object TokensLock = new();

    private static void SaveDeleteToken(string id, string token)
    {
        lock (TokensLock)
        {
            try
            {
                var map = GetSavedDeleteTokens();
                map[id] = token;
                Directory.CreateDirectory(Path.GetDirectoryName(TokensFilePath)!);
                File.WriteAllText(TokensFilePath, JsonSerializer.Serialize(map));
            }
            catch { }
        }
    }

    private static void RemoveDeleteToken(string id)
    {
        lock (TokensLock)
        {
            try
            {
                var map = GetSavedDeleteTokens();
                if (map.Remove(id))
                    File.WriteAllText(TokensFilePath, JsonSerializer.Serialize(map));
            }
            catch { }
        }
    }

    private static readonly System.Text.RegularExpressions.Regex BlockedTermsRegex = new(
        @"\b(nsfw|porn|xxx|hentai|nude|naked|sex|gore|blood|snuff|behead|dismember|erotic|18\+|onlyfans|milf|fetish|bdsm)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static bool ContainsBlockedTerms(string? text) =>
        !string.IsNullOrWhiteSpace(text) && BlockedTermsRegex.IsMatch(text);

    private static CommunityWallpaperItem ToItem(WallpaperRowDto dto) => new(
        dto.Id ?? "",
        dto.Title ?? "Untitled",
        dto.Author ?? "Anonymous",
        dto.Resolution ?? "1920x1080",
        dto.ThemeTag ?? "Custom",
        dto.ImageUrl ?? "",
        dto.Downloads,
        dto.CreatedAt);

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed class ApiListResponseDto
    {
        [JsonPropertyName("items")] public List<WallpaperRowDto>? Items { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed class ApiPublishResponseDto
    {
        [JsonPropertyName("item")] public WallpaperRowDto? Item { get; set; }
        [JsonPropertyName("token")] public string? Token { get; set; }
    }

    [Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed class WallpaperRowDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("author")] public string? Author { get; set; }
        [JsonPropertyName("resolution")] public string? Resolution { get; set; }
        [JsonPropertyName("theme_tag")] public string? ThemeTag { get; set; }
        [JsonPropertyName("image_url")] public string? ImageUrl { get; set; }
        [JsonPropertyName("downloads")] public int Downloads { get; set; }
        [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
    }
}
