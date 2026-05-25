namespace SeqMcp.Core.Configuration;

/// <summary>
/// Per-request scoped context for Seq MCP server configuration.
/// Allows HTTP headers to override default configuration values.
/// </summary>
public class SeqRequestContext
{
    /// <summary>
    /// Project scope for filtering Seq events (e.g., "MyProject").
    /// Extracted from X-Seq-Project-Scope header or null if not provided.
    /// </summary>
    public string? ProjectScope { get; set; }

    /// <summary>
    /// Field name to use for scope filtering (e.g., "Application", "Environment").
    /// Extracted from X-Seq-Scope-Field header or null if not provided.
    /// </summary>
    public string? ScopeField { get; set; }

    /// <summary>
    /// Per-request Seq URL override (from <c>X-Seq-Url</c> header).
    /// Populated by the HTTP middleware ONLY when
    /// <see cref="SeqOptions.AllowUrlOverride"/> is <c>true</c> AND the
    /// supplied URL passes structural validation (scheme, no credentials,
    /// no fragment, no null bytes). Reaches
    /// <see cref="SeqMcp.Core.Services.SeqApiClient"/>, which then produces
    /// a <see cref="TrustMode.HeaderOverride"/> endpoint — that is the
    /// trigger for the connection factory's SSRF connect-time filter.
    /// </summary>
    public string? SeqUrl { get; set; }

    /// <summary>
    /// Per-request Seq API key override (from <c>X-Seq-ApiKey</c> header).
    /// Always read by the middleware regardless of
    /// <see cref="SeqOptions.AllowUrlOverride"/>; used by
    /// <see cref="SeqMcp.Core.Services.SeqApiClient"/> in preference to
    /// <see cref="SeqOptions.ApiKey"/>.
    /// </summary>
    public string? ApiKey { get; set; }
}
