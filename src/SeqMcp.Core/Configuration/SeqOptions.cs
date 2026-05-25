namespace SeqMcp.Core.Configuration;

/// <summary>
/// Strongly-typed Seq configuration, bound via <c>SeqOptionsLoader.Load</c>
/// (or <c>SeqOptionsExtensions.AddSeqOptions</c> for DI).
///
/// Field-level priority is intentionally asymmetric to preserve the legacy
/// behavior of the original <c>Program.cs:17-31</c> loader. See
/// <see cref="SeqMcp.Core.Hosting.SeqOptionsLoader"/> for the priority table.
/// </summary>
public class SeqOptions
{
    public string Url { get; set; } = "http://localhost:8080";

    public string? ApiKey { get; set; }

    /// <summary>
    /// Default project scope for filtering events (optional).
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string? ProjectScope { get; set; }

    /// <summary>
    /// Field name to use for scope filtering (default: "Application").
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string ScopeField { get; set; } = "Application";

    /// <summary>
    /// When <c>true</c>, the connection factory's SSRF ConnectCallback
    /// (active only for <see cref="TrustMode.HeaderOverride"/> endpoints)
    /// additionally rejects RFC1918 private ranges
    /// (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16).
    /// Loopback (127.0.0.0/8, ::1) and link-local
    /// (169.254.0.0/16, fe80::/10) are always rejected for HeaderOverride.
    /// Default: <c>false</c>. Wired into the connection factory; only takes
    /// effect once <see cref="AllowUrlOverride"/> is enabled and a request
    /// supplies <c>X-Seq-Url</c>.
    /// </summary>
    public bool BlockPrivateHosts { get; set; } = false;

    /// <summary>
    /// When <c>true</c>, the HTTP middleware accepts the <c>X-Seq-Url</c>
    /// header and forwards the URL to <see cref="SeqRequestContext.SeqUrl"/>,
    /// which makes <see cref="SeqMcp.Core.Services.SeqApiClient"/> resolve
    /// to a <see cref="TrustMode.HeaderOverride"/> endpoint (activates the
    /// SSRF connect-time filter).
    /// Default: <c>false</c> — the header is logged-and-ignored, no
    /// header-driven multi-tenancy. Existing single-Seq deployments are
    /// unaffected.
    /// Never enable on a publicly reachable MCP server without an
    /// authenticating reverse proxy.
    /// </summary>
    public bool AllowUrlOverride { get; set; } = false;
}
