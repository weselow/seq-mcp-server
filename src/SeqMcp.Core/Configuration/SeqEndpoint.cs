namespace SeqMcp.Core.Configuration;

/// <summary>
/// Trust level of a Seq endpoint. Drives whether SSRF-style ConnectCallback
/// filtering is applied to outbound HTTP connections.
/// </summary>
public enum TrustMode
{
    /// <summary>
    /// Endpoint URL comes from operator-controlled configuration
    /// (<c>SeqOptions</c>, env vars, appsettings.json). Loopback /
    /// link-local destinations are allowed — it's a legitimate setup
    /// (Seq running on the same host or in the same network).
    /// </summary>
    TrustedConfig,

    /// <summary>
    /// Endpoint URL came from an external HTTP header
    /// (<c>X-Seq-Url</c>) or another untrusted source.
    /// ConnectCallback is attached to the handler and rejects connections
    /// to loopback / link-local / (optionally) RFC1918 IPs. Used in PR-5
    /// when multi-tenancy is enabled; in PR-3 no caller produces it yet.
    /// </summary>
    HeaderOverride,
}

/// <summary>
/// Describes a single Seq target — URL, optional API key, and the trust
/// level that determines whether SSRF filtering is needed.
///
/// Used as the cache key by <see cref="SeqMcp.Core.Services.ISeqConnectionFactory"/>:
/// two requests for the same (URL, ApiKey, TrustMode) share one
/// <c>HttpClient</c> + <c>SeqConnection</c> pair; different TrustMode
/// values for the same URL produce separate cache entries so a trusted
/// handler is never reused for an untrusted request.
/// </summary>
public sealed record SeqEndpoint(
    string Url,
    string? ApiKey,
    TrustMode TrustMode);
