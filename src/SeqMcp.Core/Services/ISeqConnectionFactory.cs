using SeqMcp.Core.Configuration;

namespace SeqMcp.Core.Services;

/// <summary>
/// Owns every <c>HttpClient</c>, <c>SeqConnection</c> and
/// <c>SocketsHttpHandler</c> instance in the process. Caches one client pair
/// per <see cref="SeqEndpoint"/> (URL + ApiKey + TrustMode).
///
/// Designed for both stdio (single Seq, one cache entry forever) and
/// HTTP multi-tenant (many Seqs, LRU + TTL eviction). PR-3 only wires the
/// stdio path through the factory; PR-5 will introduce header-driven
/// endpoint overrides.
///
/// Lifetime: Singleton. The factory is <see cref="IAsyncDisposable"/>; the
/// DI container disposes it at shutdown, which in turn disposes every
/// cached client pair.
/// </summary>
public interface ISeqConnectionFactory : IAsyncDisposable
{
    /// <summary>
    /// Returns a lease over the cache entry for <paramref name="endpoint"/>.
    /// Creates the entry on first call. Bumps the entry's reference counter;
    /// dispose the lease to release.
    /// </summary>
    IConnectionLease GetConnection(SeqEndpoint endpoint);
}
