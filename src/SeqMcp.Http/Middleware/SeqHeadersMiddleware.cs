using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;

namespace SeqMcp.Http.Middleware;

/// <summary>
/// Reads Seq-related request headers into the per-request
/// <see cref="SeqRequestContext"/>:
/// <list type="bullet">
///   <item><c>X-Seq-Project-Scope</c> → <see cref="SeqRequestContext.ProjectScope"/></item>
///   <item><c>X-Seq-Scope-Field</c>   → <see cref="SeqRequestContext.ScopeField"/></item>
///   <item><c>X-Seq-ApiKey</c>        → <see cref="SeqRequestContext.ApiKey"/> (always)</item>
///   <item><c>X-Seq-Url</c>           → <see cref="SeqRequestContext.SeqUrl"/>, but only when
///       <see cref="SeqOptions.AllowUrlOverride"/> is on; otherwise ignored with a single warning.</item>
/// </list>
///
/// When <c>X-Seq-Url</c> is supplied with override enabled, the URL is
/// validated structurally before it ever reaches the connection factory.
/// Failures short-circuit with <c>400 Bad Request</c> and a body that
/// does NOT echo the header value (avoiding a header-reflection leak).
///
/// The SSRF connect-time filter (which actually does the DNS resolution
/// and blocks loopback / link-local / RFC1918) lives in
/// <see cref="SeqMcp.Core.Services.SsrfConnectFilter"/> and is attached
/// to the handler by <see cref="SeqMcp.Core.Services.SeqConnectionFactory"/>
/// when the endpoint is <see cref="TrustMode.HeaderOverride"/>.
/// </summary>
public sealed class SeqHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SeqHeadersMiddleware> _logger;
    private readonly SeqOptions _options;
    private int _ignoredUrlWarningEmitted;

    public SeqHeadersMiddleware(
        RequestDelegate next,
        ILogger<SeqHeadersMiddleware> logger,
        IOptions<SeqOptions> options)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestContext = context.RequestServices.GetRequiredService<SeqRequestContext>();

        if (context.Request.Headers.TryGetValue("X-Seq-Project-Scope", out var projectScope))
        {
            requestContext.ProjectScope = projectScope.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Seq-Scope-Field", out var scopeField))
        {
            requestContext.ScopeField = scopeField.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Seq-ApiKey", out var apiKey))
        {
            requestContext.ApiKey = apiKey.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Seq-Url", out var urlHeader))
        {
            if (!_options.AllowUrlOverride)
            {
                WarnUrlOverrideDisabledOnce();
            }
            else
            {
                var validation = ValidateOverrideUrl(urlHeader.ToString());
                if (!validation.IsValid)
                {
                    await WriteBadRequest(context, validation.Error!);
                    return;
                }
                requestContext.SeqUrl = validation.NormalizedUrl;
            }
        }

        await _next(context);
    }

    private void WarnUrlOverrideDisabledOnce()
    {
        // Latch: fire once per process. Repeat clients sending the header
        // are otherwise enough to flood logs, and the message is identical.
        if (Interlocked.CompareExchange(ref _ignoredUrlWarningEmitted, 1, 0) == 0)
        {
            _logger.LogWarning(
                "X-Seq-Url header received but SEQ_ALLOW_URL_OVERRIDE=false; ignoring. "
                + "Subsequent occurrences will not be logged.");
        }
    }

    private static UrlValidation ValidateOverrideUrl(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return UrlValidation.Fail("X-Seq-Url header is empty.");
        }

        // Null-byte / control-char check on the raw string BEFORE parsing —
        // Uri can silently accept some control chars in path/query.
        if (ContainsControlOrNullByte(raw))
        {
            return UrlValidation.Fail("X-Seq-Url contains forbidden control characters.");
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return UrlValidation.Fail("X-Seq-Url is not a valid absolute URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return UrlValidation.Fail("X-Seq-Url scheme must be http or https.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return UrlValidation.Fail("X-Seq-Url must not contain user info (credentials).");
        }

        // Fragment in raw string — Uri.Fragment includes the leading '#'.
        // Even if Uri parsed it, we reject: fragments make no sense for a
        // Seq endpoint and indicate a malformed override.
        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            return UrlValidation.Fail("X-Seq-Url must not contain a fragment.");
        }

        return UrlValidation.Ok(uri.GetLeftPart(UriPartial.Path));
    }

    private static bool ContainsControlOrNullByte(string s)
    {
        foreach (var c in s)
        {
            if (c == '\0') return true;
            // Strip CR/LF (typical injection vectors) and other C0 controls.
            if (c < 0x20) return true;
            if (c == 0x7F) return true;
        }
        return false;
    }

    private static async Task WriteBadRequest(HttpContext context, string error)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        context.Response.ContentType = "application/json; charset=utf-8";
        // Body does NOT echo the offending header value — preventing
        // attackers from using the error response as a header-reflection
        // oracle.
        var body = "{\"error\":\"" + JsonEscape(error) + "\"}";
        await context.Response.WriteAsync(body);
    }

    private static string JsonEscape(string s)
    {
        // Minimal escape: backslash + quote. Our error strings are static
        // literals so no other characters appear.
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private readonly struct UrlValidation
    {
        public bool IsValid { get; }
        public string? NormalizedUrl { get; }
        public string? Error { get; }

        private UrlValidation(bool valid, string? url, string? error)
        {
            IsValid = valid;
            NormalizedUrl = url;
            Error = error;
        }

        public static UrlValidation Ok(string url) => new(true, url, null);
        public static UrlValidation Fail(string error) => new(false, null, error);
    }
}
