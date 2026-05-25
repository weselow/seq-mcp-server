using Microsoft.Extensions.Logging;
using Seq.Api;

namespace SeqMcp.Core.Services;

/// <summary>
/// One cache entry: owns its <see cref="SocketsHttpHandler"/>,
/// <see cref="HttpClient"/> and <see cref="SeqConnection"/>. Tracks
/// reference count (active leases) and an <c>Expired</c> flag set by the
/// factory on LRU/TTL eviction. Real disposal happens when the entry is
/// expired and the last lease is released (or when the grace period
/// elapses — the factory forces it).
/// </summary>
internal sealed class CachedSeqClient
{
    private readonly SocketsHttpHandler _handler;
    private readonly HttpClient _httpClient;
    private readonly SeqConnection _seqConnection;
    private readonly IClock _clock;
    private readonly Action<CachedSeqClient> _onReleased;
    private readonly ILogger<CachedSeqClient> _logger;
    private int _refCount;
    private int _expired;
    private int _disposed;
    private long _lastAccessTicks;
    private long _expiredAtTicks;

    public CachedSeqClient(
        CacheKey key,
        SocketsHttpHandler handler,
        HttpClient httpClient,
        SeqConnection seqConnection,
        IClock clock,
        Action<CachedSeqClient> onReleased,
        ILogger<CachedSeqClient> logger,
        DateTime createdAt)
    {
        Key = key;
        _handler = handler;
        _httpClient = httpClient;
        _seqConnection = seqConnection;
        _clock = clock;
        _onReleased = onReleased;
        _logger = logger;
        _lastAccessTicks = createdAt.Ticks;
    }

    public CacheKey Key { get; }
    public HttpClient HttpClient => _httpClient;
    public SeqConnection SeqConnection => _seqConnection;
    public int RefCount => Volatile.Read(ref _refCount);
    public bool IsExpired => Volatile.Read(ref _expired) == 1;
    public bool IsDisposed => Volatile.Read(ref _disposed) == 1;
    public DateTime LastAccess => new(Interlocked.Read(ref _lastAccessTicks), DateTimeKind.Utc);
    public DateTime ExpiredAt => new(Interlocked.Read(ref _expiredAtTicks), DateTimeKind.Utc);

    /// <summary>Returns null if the entry is already expired — caller should retry with a fresh entry.</summary>
    public IConnectionLease? TryAcquire(DateTime nowUtc)
    {
        if (IsExpired || IsDisposed) return null;
        Interlocked.Increment(ref _refCount);
        // Re-check after increment — eviction might have raced.
        if (IsExpired || IsDisposed)
        {
            Release();
            return null;
        }
        Interlocked.Exchange(ref _lastAccessTicks, nowUtc.Ticks);
        return new Lease(this);
    }

    public bool MarkExpired(DateTime nowUtc)
    {
        if (Interlocked.Exchange(ref _expired, 1) == 1) return false;
        Interlocked.Exchange(ref _expiredAtTicks, nowUtc.Ticks);
        return true;
    }

    private void Release()
    {
        var remaining = Interlocked.Decrement(ref _refCount);
        if (remaining < 0)
        {
            // Should never happen; clamp and log so we don't lose track.
            Interlocked.Exchange(ref _refCount, 0);
            _logger.LogError("CachedSeqClient {Key} refCount went negative", Key);
            return;
        }
        if (remaining == 0 && IsExpired)
        {
            _ = DisposeIfIdleAsync();
        }
    }

    public async ValueTask DisposeIfIdleAsync()
    {
        if (RefCount != 0) return;
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        await DisposeInternalAsync().ConfigureAwait(false);
        _onReleased(this);
    }

    public async ValueTask ForceDisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
        await DisposeInternalAsync().ConfigureAwait(false);
        _onReleased(this);
    }

    private ValueTask DisposeInternalAsync()
    {
        // SeqConnection internally disposes the HttpClient + handler it owns.
        // OUR HttpClient + handler are separate (we built them ourselves and
        // pass nothing to SeqConnection); we dispose them here.
        // SeqConnection.Dispose() is idempotent (verified by smoke test).
        try
        {
            _seqConnection.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SeqConnection.Dispose threw — swallowed");
        }
        _httpClient.Dispose();
        _handler.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Lease : IConnectionLease
    {
        private readonly CachedSeqClient _owner;
        private int _disposed;

        public Lease(CachedSeqClient owner) => _owner = owner;

        public HttpClient HttpClient => _owner._httpClient;
        public SeqConnection SeqConnection => _owner._seqConnection;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return ValueTask.CompletedTask;
            _owner.Release();
            return ValueTask.CompletedTask;
        }
    }
}
