using System.Text.RegularExpressions;

namespace SeqMcp.Middleware;

public static class SensitiveDataMasker
{
    private const string Placeholder = "***";

    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-Seq-ApiKey",
        "Cookie",
        "Set-Cookie",
        "Proxy-Authorization",
    };

    private static readonly Regex SensitiveJsonField = new(
        "\"(api[_-]?key|authorization|token|password|secret)\"(\\s*:\\s*)\"(?:\\\\.|[^\"\\\\])*\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Mask(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return SensitiveJsonField.Replace(input, $"\"$1\"$2\"{Placeholder}\"");
    }

    public static string MaskHeaderValue(string headerName, string value)
    {
        return SensitiveHeaderNames.Contains(headerName) ? Placeholder : value;
    }
}
