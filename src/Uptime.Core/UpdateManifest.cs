using System.Reflection;
using System.Text.RegularExpressions;

namespace Uptime.Core;

[Obfuscation(Exclude = true, ApplyToMembers = true)]
public sealed record UpdateManifest(
    string version,
    string downloadUrl,
    string sha256,
    string? notes = null);

public static partial class UpdateManifestValidator
{
    public const string TrustedDownloadPrefix = "https://uptime-pc.vercel.app/";

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex Sha256HexRegex();

    public static bool IsNewerVersion(string? currentVersionText, string? candidateVersionText)
    {
        if (!TryParseNormalizedVersion(currentVersionText, out var current))
            return false;
        if (!TryParseNormalizedVersion(candidateVersionText, out var candidate))
            return false;
        return candidate > current;
    }

    public static bool TryParseNormalizedVersion(string? text, out Version version)
    {
        version = new Version(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var cleaned = text.Trim();
        if (cleaned.StartsWith('v') || cleaned.StartsWith('V'))
            cleaned = cleaned[1..];

        var plusIdx = cleaned.IndexOf('+');
        if (plusIdx >= 0) cleaned = cleaned[..plusIdx];
        if (cleaned.Contains('-'))
            return false;

        if (!Version.TryParse(cleaned, out var parsed) || parsed is null)
            return false;

        version = new Version(
            Math.Max(0, parsed.Major),
            Math.Max(0, parsed.Minor),
            Math.Max(0, parsed.Build),
            Math.Max(0, parsed.Revision));
        return true;
    }

    public static bool IsValidManifest(UpdateManifest? manifest)
    {
        if (manifest is null) return false;
        if (!TryParseNormalizedVersion(manifest.version, out _)) return false;
        if (string.IsNullOrWhiteSpace(manifest.downloadUrl)) return false;

        var url = manifest.downloadUrl.Trim();
        if (url.Any(char.IsWhiteSpace) ||
            !url.StartsWith(TrustedDownloadPrefix, StringComparison.OrdinalIgnoreCase) ||
            url.Length <= TrustedDownloadPrefix.Length ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
            return false;

        if (string.IsNullOrWhiteSpace(manifest.sha256) || !Sha256HexRegex().IsMatch(manifest.sha256.Trim()))
            return false;
        return true;
    }
}
