using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Uptime.Presentation.Services;

/// <summary>
/// Lightweight, dependency-free Discord Desktop Rich Presence client over local IPC named pipes.
/// Uses explicit JsonObject keys so obfuscation never renames IPC protocol fields.
/// </summary>
public sealed class DiscordRpcService : IDisposable
{
    private const int OpHandshake = 0;
    private const int OpFrame = 1;
    private const int OpClose = 2;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private NamedPipeClientStream? _pipe;
    private string _connectedClientId = "";
    private bool _disposed;

    public bool IsConnected => _pipe is { IsConnected: true };
    public string ConnectedUsername { get; private set; } = "";
    public string ConnectedUserId { get; private set; } = "";
    public string LastError { get; private set; } = "";

    public event Action? StateChanged;

    public async Task UpdatePresenceAsync(
        string clientId,
        string details,
        string state,
        long startUnixSeconds,
        CancellationToken ct = default)
    {
        if (_disposed) return;

        var normalizedId = (clientId ?? "").Trim();
        if (!IsValidClientId(normalizedId))
        {
            await DisconnectInternalAsync();
            LastError = "MissingAppId";
            StateChanged?.Invoke();
            return;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_disposed) return;

            if (_pipe is not { IsConnected: true } || _connectedClientId != normalizedId)
            {
                await ConnectLockedAsync(normalizedId, ct);
            }

            if (_pipe is not { IsConnected: true }) return;

            var payload = new JsonObject
            {
                ["cmd"] = "SET_ACTIVITY",
                ["args"] = new JsonObject
                {
                    ["pid"] = Environment.ProcessId,
                    ["activity"] = new JsonObject
                    {
                        ["details"] = ClampText(details, 128),
                        ["state"] = ClampText(state, 128),
                        ["timestamps"] = new JsonObject
                        {
                            ["start"] = startUnixSeconds
                        },
                        ["assets"] = new JsonObject
                        {
                            ["large_image"] = "uptime_logo",
                            ["large_text"] = "Uptime — PC Monitor",
                            ["small_image"] = "uppy",
                            ["small_text"] = "Uppy"
                        },
                        ["buttons"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["label"] = "Uptime for Windows",
                                ["url"] = "https://uptime-pc.vercel.app"
                            }
                        }
                    }
                },
                ["nonce"] = Guid.NewGuid().ToString("N")
            };

            await WriteFrameLockedAsync(OpFrame, payload.ToJsonString(), ct);
            _ = await TryReadFrameLockedAsync(TimeSpan.FromMilliseconds(400), ct);
            LastError = "";
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or TimeoutException)
        {
            ClosePipeLocked();
            LastError = "WaitingForDiscord";
        }
        catch (OperationCanceledException)
        {
            // Ignore cancellation
        }
        catch (Exception ex)
        {
            ClosePipeLocked();
            LastError = ex.Message;
        }
        finally
        {
            _gate.Release();
            StateChanged?.Invoke();
        }
    }

    public async Task ClearPresenceAsync()
    {
        if (_disposed) return;

        await _gate.WaitAsync();
        try
        {
            if (_pipe is { IsConnected: true })
            {
                var payload = new JsonObject
                {
                    ["cmd"] = "SET_ACTIVITY",
                    ["args"] = new JsonObject
                    {
                        ["pid"] = Environment.ProcessId,
                        ["activity"] = null
                    },
                    ["nonce"] = Guid.NewGuid().ToString("N")
                };
                try
                {
                    await WriteFrameLockedAsync(OpFrame, payload.ToJsonString(), CancellationToken.None);
                }
                catch
                {
                    // Best-effort clear
                }
            }
            ClosePipeLocked();
            LastError = "";
        }
        finally
        {
            _gate.Release();
            StateChanged?.Invoke();
        }
    }

    private async Task ConnectLockedAsync(string clientId, CancellationToken ct)
    {
        ClosePipeLocked();

        for (int i = 0; i < 10; i++)
        {
            ct.ThrowIfCancellationRequested();
            NamedPipeClientStream? candidate = null;
            try
            {
                candidate = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                await candidate.ConnectAsync(350, ct);

                _pipe = candidate;
                _connectedClientId = clientId;

                var handshake = new JsonObject
                {
                    ["v"] = 1,
                    ["client_id"] = clientId
                };
                await WriteFrameLockedAsync(OpHandshake, handshake.ToJsonString(), ct);

                var reply = await TryReadFrameLockedAsync(TimeSpan.FromMilliseconds(1200), ct);
                if (reply is { OpCode: OpClose })
                {
                    ClosePipeLocked();
                    LastError = "InvalidAppId";
                    return;
                }

                if (reply is { OpCode: OpFrame, Json: not null })
                {
                    ParseReadyPayload(reply.Value.Json);
                }

                LastError = "";
                return;
            }
            catch (TimeoutException)
            {
                candidate?.Dispose();
            }
            catch (IOException)
            {
                candidate?.Dispose();
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }
        }

        LastError = "WaitingForDiscord";
    }

    private void ParseReadyPayload(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("evt", out var evt) && evt.GetString() == "ERROR")
            {
                ClosePipeLocked();
                LastError = "InvalidAppId";
                return;
            }

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("user", out var user))
            {
                ConnectedUserId = user.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                var global = user.TryGetProperty("global_name", out var gEl) ? gEl.GetString() : null;
                var uname = user.TryGetProperty("username", out var uEl) ? uEl.GetString() : null;
                ConnectedUsername = !string.IsNullOrWhiteSpace(global) ? global : (uname ?? "");
            }
        }
        catch
        {
            // Non-fatal
        }
    }

    private async Task WriteFrameLockedAsync(int opCode, string json, CancellationToken ct)
    {
        if (_pipe == null) return;
        var body = Encoding.UTF8.GetBytes(json);
        var packet = new byte[8 + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(0, 4), opCode);
        BinaryPrimitives.WriteInt32LittleEndian(packet.AsSpan(4, 4), body.Length);
        Buffer.BlockCopy(body, 0, packet, 8, body.Length);

        await _pipe.WriteAsync(packet, ct);
        await _pipe.FlushAsync(ct);
    }

    private async Task<(int OpCode, string? Json)?> TryReadFrameLockedAsync(TimeSpan timeout, CancellationToken ct)
    {
        if (_pipe is not { IsConnected: true }) return null;

        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readCts.CancelAfter(timeout);

        try
        {
            var header = new byte[8];
            var read = await ReadExactAsync(_pipe, header, readCts.Token);
            if (read < 8) return null;

            int opCode = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(0, 4));
            int len = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(4, 4));
            if (len < 0 || len > 1024 * 1024) return (opCode, null);

            var payload = new byte[len];
            if (len > 0)
            {
                var bodyRead = await ReadExactAsync(_pipe, payload, readCts.Token);
                if (bodyRead < len) return null;
            }

            return (opCode, Encoding.UTF8.GetString(payload));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct);
            if (n <= 0) break;
            total += n;
        }
        return total;
    }

    private async Task DisconnectInternalAsync()
    {
        await _gate.WaitAsync();
        try
        {
            ClosePipeLocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ClosePipeLocked()
    {
        try { _pipe?.Dispose(); } catch { }
        _pipe = null;
        _connectedClientId = "";
        ConnectedUsername = "";
        ConnectedUserId = "";
    }

    private static bool IsValidClientId(string id) =>
        id.Length >= 17 && id.Length <= 22 && id.All(char.IsAsciiDigit);

    private static string ClampText(string text, int maxLen)
    {
        var t = (text ?? "").Trim();
        if (t.Length < 2) t = "Uptime";
        return t.Length <= maxLen ? t : t[..(maxLen - 1)] + "…";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClosePipeLocked();
        _gate.Dispose();
    }
}
