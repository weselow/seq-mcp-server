using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Seq.Api;
using SeqMcp.Core.Configuration;

namespace SeqMcp.Core.Services;

/// <summary>
/// Singleton factory caching one <c>HttpClient</c> + <c>SeqConnection</c>
/// pair per <see cref="SeqEndpoint"/>. See <see cref="ISeqConnectionFactory"/>
/// for the contract and the bead design notes for the architectural rationale.
///
/// Cache key: <c>(normalizedUrl, apiKey, trustMode)</c>. Each entry owns its
/// own <see cref="SocketsHttpHandler"/> (sharing a handler is impossible —
/// <c>SeqConnection.Dispose()</c> disposes the handler attached to its
/// internal client). Eviction is sliding-TTL + LRU; an evicted entry whose
/// reference count is still positive lingers in a <c>_pendingDisposal</c>
/// list and is force-disposed after a grace period.
/// </summary>
public sealed class SeqConnectionFactory : ISeqConnectionFactory
{
    private const int DefaultMaxEntries = 50;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DefaultGrace = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<CacheKey, CachedSeqClient> _cache = new();
    private readonly ConcurrentBag<CachedSeqClient> _pendingDisposal = new();
    private readonly SeqOptions _options;
    private readonly ILogger<SeqConnectionFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IClock _clock;
    private readonly int _maxEntries;
    private readonly TimeSpan _ttl;
    private readonly TimeSpan _grace;
    private readonly object _evictionLock = new();
    private int _disposed;

    public SeqConnectionFactory(
        IOptions<SeqOptions> options,
        ILogger<SeqConnectionFactory> logger,
        ILoggerFactory loggerFactory)
        : this(options, logger, loggerFactory, SystemClock.Instance, DefaultMaxEntries, DefaultTtl, DefaultGrace)
    {
    }

    internal SeqConnectionFactory(
        IOptions<SeqOptions> options,
        ILogger<SeqConnectionFactory> logger,
        ILoggerFactory loggerFactory,
        IClock clock,
        int maxEntries,
        TimeSpan ttl,
        TimeSpan grace)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        if (maxEntries < 1) throw new ArgumentOutOfRangeException(nameof(maxEntries));
        _maxEntries = maxEntries;
        _ttl = ttl;
        _grace = grace;
    }

    public IConnectionLease GetConnection(SeqEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ThrowIfDisposed();

        DrainPendingDisposal();
        EnforceTtl();

        var key = CacheKey.From(endpoint);
        var entry = _cache.GetOrAdd(key, k => CreateEntry(k, endpoint));
        var lease = entry.TryAcquire(_clock.UtcNow);

        // Race: entry was evicted between GetOrAdd and TryAcquire — retry once.
        if (lease is null)
        {
            entry = _cache.GetOrAdd(key, k => CreateEntry(k, endpoint));
            lease = entry.TryAcquire(_clock.UtcNow)
                ?? throw new InvalidOperationException("Unable to acquire connection lease.");
        }

        EnforceLruIfNeeded();
        return lease;
    }

    private CachedSeqClient CreateEntry(CacheKey key, SeqEndpoint endpoint)
    {
        _logger.LogDebug("Creating Seq client entry for {Url} (TrustMode={TrustMode})",
            endpoint.Url, endpoint.TrustMode);

        var handler = BuildHandler(endpoint);
        var http = BuildHttpClient(handler, endpoint);
        var seq = new SeqConnection(endpoint.Url, endpoint.ApiKey);
        return new CachedSeqClient(
            key,
            handler,
            http,
            seq,
            _clock,
            ReleaseEntry,
            _loggerFactory.CreateLogger<CachedSeqClient>(),
            createdAt: _clock.UtcNow);
    }

    private SocketsHttpHandler BuildHandler(SeqEndpoint endpoint)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 10,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            ResponseDrainTimeout = TimeSpan.FromSeconds(5),
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression =
                DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        if (endpoint.TrustMode == TrustMode.HeaderOverride)
        {
            var filter = new SsrfConnectFilter(_options.BlockPrivateHosts, _logger);
            handler.ConnectCallback = filter.ConnectAsync;
        }

        return handler;
    }

    private static HttpClient BuildHttpClient(SocketsHttpHandler handler, SeqEndpoint endpoint)
    {
        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(30),
            BaseAddress = new Uri(endpoint.Url),
        };

        if (!string.IsNullOrEmpty(endpoint.ApiKey))
        {
            client.DefaultRequestHeaders.Add("X-Seq-ApiKey", endpoint.ApiKey);
        }

        return client;
    }

    private void ReleaseEntry(CachedSeqClient entry)
    {
        // Called by the entry when refCount drops to zero AFTER it was
        // marked expired. Remove from main cache only if still mapped to
        // the same instance (a fresh entry for the same key may already
        // exist).
        _cache.TryRemove(new KeyValuePair<CacheKey, CachedSeqClient>(entry.Key, entry));
    }

    private void EnforceTtl()
    {
        var now = _clock.UtcNow;
        foreach (var kvp in _cache)
        {
            var entry = kvp.Value;
            if (now - entry.LastAccess > _ttl)
            {
                EvictEntry(kvp.Key, entry, reason: "ttl");
            }
        }
    }

    private void EnforceLruIfNeeded()
    {
        if (_cache.Count <= _maxEntries) return;
        lock (_evictionLock)
        {
            while (_cache.Count > _maxEntries)
            {
                var oldest = FindOldestEntry();
                if (oldest is null) break;
                EvictEntry(oldest.Value.Key, oldest.Value.Value, reason: "lru");
            }
        }
    }

    private KeyValuePair<CacheKey, CachedSeqClient>? FindOldestEntry()
    {
        KeyValuePair<CacheKey, CachedSeqClient>? oldest = null;
        var oldestTime = DateTime.MaxValue;
        foreach (var kvp in _cache)
        {
            if (kvp.Value.LastAccess < oldestTime)
            {
                oldestTime = kvp.Value.LastAccess;
                oldest = kvp;
            }
        }
        return oldest;
    }

    private void EvictEntry(CacheKey key, CachedSeqClient entry, string reason)
    {
        if (!_cache.TryRemove(new KeyValuePair<CacheKey, CachedSeqClient>(key, entry)))
        {
            return;
        }

        _logger.LogDebug("Evicting cache entry {Key} (reason: {Reason}, refCount: {RefCount})",
            key, reason, entry.RefCount);

        if (!entry.MarkExpired(_clock.UtcNow))
        {
            return; // already disposed via lease release
        }

        if (entry.RefCount == 0)
        {
            _ = entry.DisposeIfIdleAsync();
        }
        else
        {
            _pendingDisposal.Add(entry);
        }
    }

    private void DrainPendingDisposal()
    {
        if (_pendingDisposal.IsEmpty) return;
        var now = _clock.UtcNow;
        var remaining = new List<CachedSeqClient>();
        while (_pendingDisposal.TryTake(out var entry))
        {
            if (entry.IsDisposed)
            {
                continue;
            }

            var elapsed = now - entry.ExpiredAt;
            if (entry.RefCount == 0)
            {
                _ = entry.DisposeIfIdleAsync();
            }
            else if (elapsed >= _grace)
            {
                _logger.LogWarning(
                    "Force-disposing expired Seq cache entry after grace period (RefCount={RefCount})",
                    entry.RefCount);
                _ = entry.ForceDisposeAsync();
            }
            else
            {
                remaining.Add(entry);
            }
        }
        foreach (var entry in remaining)
        {
            _pendingDisposal.Add(entry);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;

        foreach (var kvp in _cache)
        {
            kvp.Value.MarkExpired(_clock.UtcNow);
            await kvp.Value.ForceDisposeAsync().ConfigureAwait(false);
        }
        _cache.Clear();

        while (_pendingDisposal.TryTake(out var entry))
        {
            await entry.ForceDisposeAsync().ConfigureAwait(false);
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) == 1)
        {
            throw new ObjectDisposedException(nameof(SeqConnectionFactory));
        }
    }

    internal int CacheCount => _cache.Count;
    internal int PendingDisposalCount => _pendingDisposal.Count;
}
