using SeqMcp.Core.Configuration;

namespace SeqMcp.Core.Services;

/// <summary>
/// Cache key for <see cref="SeqConnectionFactory"/>. URL is normalized
/// (see <see cref="UrlNormalizer.Normalize"/>), so cosmetic variants like
/// <c>https://Host/</c> and <c>https://host</c> map to the same key.
/// TrustMode is part of the key so a trusted handler is never reused
/// for an untrusted endpoint with the same URL+ApiKey.
/// </summary>
internal readonly record struct CacheKey(
    string NormalizedUrl,
    string? ApiKey,
    TrustMode TrustMode)
{
    public static CacheKey From(SeqEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        var normalized = UrlNormalizer.Normalize(endpoint.Url);
        return new CacheKey(normalized, endpoint.ApiKey, endpoint.TrustMode);
    }

    public override string ToString() =>
        $"{NormalizedUrl}|{(ApiKey is null ? "<no-key>" : "***")}|{TrustMode}";
}
