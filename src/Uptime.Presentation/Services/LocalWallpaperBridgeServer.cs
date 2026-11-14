using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia.Threading;
using Uptime.Core;
using Uptime.Presentation.ViewModels;

namespace Uptime.Presentation.Services;

/// <summary>
/// Lightweight loopback HTTP endpoint so the official Uptime website can apply
/// a community wallpaper in real time inside the running desktop app.
/// </summary>
public sealed class LocalWallpaperBridgeServer : IDisposable
{
    private const string Prefix = "http://127.0.0.1:47621/";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly ICommunityWallpaperService _wallpaperService;
    private readonly SettingsViewModel _settings;

    public LocalWallpaperBridgeServer(ICommunityWallpaperService wallpaperService, SettingsViewModel settings)
    {
        _wallpaperService = wallpaperService;
        _settings = settings;
    }

    public void Start()
    {
        try
        {
            _listener.Prefixes.Add(Prefix);
            _listener.Start();
            _ = Task.Run(ListenLoopAsync);
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "LocalWallpaperBridgeServer could not start.", ex);
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(ctx));
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var resp = ctx.Response;
        AddCorsHeaders(resp);

        try
        {
            if (req.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                resp.StatusCode = 204;
                resp.Close();
                return;
            }

            if (req.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                req.Url?.AbsolutePath.TrimEnd('/') == "/own-tokens")
            {
                var tokens = Uptime.Infrastructure.SupabaseWallpaperService.GetSavedDeleteTokens();
                var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tokens));
                resp.StatusCode = 200;
                resp.ContentType = "application/json";
                await resp.OutputStream.WriteAsync(jsonBytes);
                resp.Close();
                return;
            }

            if (req.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
                req.Url?.AbsolutePath.TrimEnd('/') == "/apply-wallpaper")
            {
                using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var payload = JsonSerializer.Deserialize<ApplyPayload>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (payload is null || string.IsNullOrWhiteSpace(payload.ImageUrl) ||
                    !Uri.TryCreate(payload.ImageUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                {
                    resp.StatusCode = 400;
                    resp.Close();
                    return;
                }

                var item = new CommunityWallpaperItem(
                    string.IsNullOrWhiteSpace(payload.Id) ? Guid.NewGuid().ToString("N") : payload.Id,
                    payload.Title ?? "Community Wallpaper",
                    payload.Author ?? "Anonymous",
                    payload.Resolution ?? "1920x1080",
                    payload.ThemeTag ?? "Community",
                    payload.ImageUrl,
                    payload.Downloads,
                    DateTimeOffset.UtcNow);

                var localPath = await _wallpaperService.DownloadWallpaperToTempAsync(item, countDownload: false, cancellationToken: _cts.Token);
                Dispatcher.UIThread.Post(() =>
                {
                    _settings.SetCustomBackground(localPath);
                    _settings.CommunityStatus = Localizer.Instance.Format("CommunityApplied", item.Author);
                });

                var okBytes = Encoding.UTF8.GetBytes("{\"ok\":true}");
                resp.StatusCode = 200;
                resp.ContentType = "application/json";
                await resp.OutputStream.WriteAsync(okBytes);
                resp.Close();
                return;
            }

            resp.StatusCode = 404;
            resp.Close();
        }
        catch (Exception ex)
        {
            AppDiagnostics.Write("Warning", "LocalWallpaperBridgeServer request failed.", ex);
            try
            {
                resp.StatusCode = 500;
                resp.Close();
            }
            catch { }
        }
    }

    private static void AddCorsHeaders(HttpListenerResponse resp)
    {
        resp.Headers["Access-Control-Allow-Origin"] = "*";
        resp.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        resp.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        resp.Headers["Access-Control-Allow-Private-Network"] = "true";
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener.Stop(); } catch { }
        try { _listener.Close(); } catch { }
        _cts.Dispose();
    }

    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    private sealed class ApplyPayload
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Resolution { get; set; }
        public string? ThemeTag { get; set; }
        public string? ImageUrl { get; set; }
        public int Downloads { get; set; }
    }
}
