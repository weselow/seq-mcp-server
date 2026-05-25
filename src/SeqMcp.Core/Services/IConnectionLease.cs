using Seq.Api;

namespace SeqMcp.Core.Services;

/// <summary>
/// Reference-counted handle to a cached Seq client pair (<c>HttpClient</c> +
/// <c>SeqConnection</c>). Returned by <see cref="ISeqConnectionFactory.GetConnection"/>.
///
/// While the lease is alive, the underlying cache entry will not be disposed
/// — even if LRU/TTL eviction marks it as expired. On <c>DisposeAsync</c>
/// the reference counter is decremented; when it reaches zero on an expired
/// entry, the entry is disposed.
///
/// Usage:
/// <code>
/// var endpoint = new SeqEndpoint(url, apiKey, TrustMode.TrustedConfig);
/// await using var lease = factory.GetConnection(endpoint);
/// var response = await lease.HttpClient.GetAsync("/api/events");
/// var signals = await lease.SeqConnection.Signals.ListAsync();
/// </code>
/// </summary>
public interface IConnectionLease : IAsyncDisposable
{
    HttpClient HttpClient { get; }
    SeqConnection SeqConnection { get; }
}
