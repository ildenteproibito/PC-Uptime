using System.Text.Json;

namespace Uptime.Core;

public enum LoadStatus { Ok, Missing, RecoveredFromBackup, Unrecoverable }

public sealed record LoadResult<T>(T? Value, LoadStatus Status, IReadOnlyList<string> QuarantinedFiles) where T : class;

/// <summary>
/// Atomic persistence: writes to .tmp, then File.Replace with backup (.bak).
/// Never silently discards data: unreadable files are quarantined (.corrupt-*).
/// </summary>
public sealed class AtomicJsonStore<T> where T : class
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;
    private readonly object _sync = new();

    public AtomicJsonStore(string path, JsonSerializerOptions? options = null)
    {
        _path = path;
        _options = options ?? new JsonSerializerOptions { WriteIndented = true };
    }

    public string BackupPath => _path + ".bak";
    private string TempPath => _path + ".tmp";

    public void Save(T value)
    {
        lock (_sync)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _options);
            using (var fs = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(true); // force write to disk before swap
            }

            if (File.Exists(_path))
                File.Replace(TempPath, _path, BackupPath, ignoreMetadataErrors: true);
            else
                File.Move(TempPath, _path);
        }
    }

    public LoadResult<T> Load()
    {
        lock (_sync)
        {
            TryDelete(TempPath); // remnant of an interrupted write: primary was untouched

            var primaryExists = File.Exists(_path);
            var backupExists = File.Exists(BackupPath);
            if (!primaryExists && !backupExists)
                return new LoadResult<T>(null, LoadStatus.Missing, Array.Empty<string>());

            if (primaryExists && TryRead(_path) is { } ok)
                return new LoadResult<T>(ok, LoadStatus.Ok, Array.Empty<string>());

            var quarantined = new List<string>();
            if (primaryExists && Quarantine(_path) is { } qp) quarantined.Add(qp);

            if (backupExists && TryRead(BackupPath) is { } fromBackup)
            {
                try
                {
                    // Write backup atomically via TempPath so power loss mid-copy never truncates _path
                    File.Copy(BackupPath, TempPath, overwrite: true);
                    File.Move(TempPath, _path, overwrite: true);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    TryDelete(TempPath);
                }
                return new LoadResult<T>(fromBackup, LoadStatus.RecoveredFromBackup, quarantined);
            }

            if (backupExists && Quarantine(BackupPath) is { } qb) quarantined.Add(qb);
            return new LoadResult<T>(null, LoadStatus.Unrecoverable, quarantined);
        }
    }

    private T? TryRead(string file)
    {
        try
        {
            using var fs = File.OpenRead(file);
            return JsonSerializer.Deserialize<T>(fs, _options);
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }

    private static string? Quarantine(string file)
    {
        try
        {
            var target = $"{file}.corrupt-{DateTime.Now:yyyyMMddHHmmssfff}";
            File.Move(file, target, overwrite: true);
            return target;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
    }
}
