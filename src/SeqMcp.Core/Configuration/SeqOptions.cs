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
    /// Default: <c>false</c>. Has no effect in PR-3 because no
    /// <c>HeaderOverride</c> endpoints are produced yet (PR-5 wires that up).
    /// </summary>
    public bool BlockPrivateHosts { get; set; } = false;
}
