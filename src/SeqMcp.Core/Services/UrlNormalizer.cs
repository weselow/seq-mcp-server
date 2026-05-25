using System.Text.RegularExpressions;

namespace SeqMcp.Core.Services;

/// <summary>
/// Normalizes Seq base URLs for cache-key equality (see bead design notes,
/// Decision 4). The algorithm intentionally preserves the path so two
/// path-scoped Seqs (<c>https://host/seq-a</c> vs <c>https://host/seq-b</c>)
/// remain distinct cache entries.
///
/// Rules:
/// <list type="bullet">
///   <item>Scheme + host lowercased.</item>
///   <item>Trailing slash stripped from path.</item>
///   <item>Query, fragment, userinfo stripped.</item>
///   <item>Default port stripped (<c>:80</c> for http, <c>:443</c> for https).</item>
///   <item>Percent-encoded sequences lowercased (<c>%2F</c> → <c>%2f</c>).</item>
/// </list>
/// </summary>
internal static class UrlNormalizer
{
    private static readonly Regex PercentEncodedRegex = new(
        @"%[0-9A-Fa-f]{2}",
        RegexOptions.Compiled);

    public static string Normalize(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(url));
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"Invalid URL: {url}", nameof(url));
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var path = TrimTrailingSlash(uri.AbsolutePath);
        path = LowercasePercentEncoded(path);

        var portSegment = IsDefaultPort(scheme, uri.Port) ? string.Empty : $":{uri.Port}";
        return $"{scheme}://{host}{portSegment}{path}";
    }

    private static string TrimTrailingSlash(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return string.Empty;
        }
        return path.EndsWith('/') ? path[..^1] : path;
    }

    private static string LowercasePercentEncoded(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return PercentEncodedRegex.Replace(value, m => m.Value.ToLowerInvariant());
    }

    private static bool IsDefaultPort(string scheme, int port) =>
        (scheme == "http" && port == 80) || (scheme == "https" && port == 443);
}
