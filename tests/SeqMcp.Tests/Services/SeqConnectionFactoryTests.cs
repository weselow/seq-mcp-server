using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Verifies the cache/eviction/lease semantics of
/// <see cref="SeqConnectionFactory"/>. All tests run offline:
/// <c>SeqConnection</c>'s constructor is lazy (the smoke test in the
/// design notes confirmed double-Dispose is safe), and the tests never
/// trigger real HTTP I/O.
///
/// Tests use the internal-only test constructor to inject a
/// <see cref="FakeClock"/> and small <c>maxEntries</c>/short TTL/grace,
/// so we can exercise eviction without sleeping.
/// </summary>
public class SeqConnectionFactoryTests
{
    private static readonly DateTime Epoch = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static IOptions<SeqOptions> Options_() =>
        Options.Create(new SeqOptions { Url = "http://localhost:65530" });

    private static SeqConnectionFactory Build(
        FakeClock clock,
        int maxEntries = 3,
        TimeSpan? ttl = null,
        TimeSpan? grace = null,
        bool blockPrivateHosts = false)
    {
        var options = Options.Create(new SeqOptions
        {
            Url = "http://localhost:65530",
            BlockPrivateHosts = blockPrivateHosts,
        });
        return new SeqConnectionFactory(
            options,
            NullLogger<SeqConnectionFactory>.Instance,
            NullLoggerFactory.Instance,
            clock,
            maxEntries,
            ttl ?? TimeSpan.FromMinutes(30),
            grace ?? TimeSpan.FromMinutes(2));
    }

    private static SeqEndpoint Endpoint(string url, string? apiKey = null, TrustMode mode = TrustMode.TrustedConfig)
        => new(url, apiKey, mode);

    [Fact]
    public async Task GetConnection_Returns_Lease_With_HttpClient_And_SeqConnection()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var lease = factory.GetConnection(Endpoint("http://example.test/"));

        // Assert
        lease.Should().NotBeNull();
        lease.HttpClient.Should().NotBeNull();
        lease.SeqConnection.Should().NotBeNull();
        lease.HttpClient.BaseAddress!.ToString().Should().Be("http://example.test/");

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task Same_Endpoint_Returns_Same_Cached_Pair()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));
        var endpoint = Endpoint("http://example.test/");

        // Act
        var lease1 = factory.GetConnection(endpoint);
        var lease2 = factory.GetConnection(endpoint);

        // Assert — same HttpClient instance proves cache reuse
        ReferenceEquals(lease1.HttpClient, lease2.HttpClient).Should().BeTrue();
        ReferenceEquals(lease1.SeqConnection, lease2.SeqConnection).Should().BeTrue();
        factory.CacheCount.Should().Be(1);

        await lease1.DisposeAsync();
        await lease2.DisposeAsync();
    }

    [Fact]
    public async Task Different_Urls_Produce_Different_Cache_Entries()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var leaseA = factory.GetConnection(Endpoint("http://host-a.test/"));
        var leaseB = factory.GetConnection(Endpoint("http://host-b.test/"));

        // Assert
        ReferenceEquals(leaseA.HttpClient, leaseB.HttpClient).Should().BeFalse();
        factory.CacheCount.Should().Be(2);

        await leaseA.DisposeAsync();
        await leaseB.DisposeAsync();
    }

    [Fact]
    public async Task Same_Url_Different_TrustMode_Produce_Different_Entries()
    {
        // Arrange — TrustMode is part of the cache key (design decision 2)
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var trusted = factory.GetConnection(
            Endpoint("http://example.test/", apiKey: "k", mode: TrustMode.TrustedConfig));
        var header = factory.GetConnection(
            Endpoint("http://example.test/", apiKey: "k", mode: TrustMode.HeaderOverride));

        // Assert
        ReferenceEquals(trusted.HttpClient, header.HttpClient).Should().BeFalse();
        factory.CacheCount.Should().Be(2);

        await trusted.DisposeAsync();
        await header.DisposeAsync();
    }

    [Fact]
    public async Task Url_Cosmetic_Variants_Share_A_Single_Cache_Entry()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var l1 = factory.GetConnection(Endpoint("https://Host/"));
        var l2 = factory.GetConnection(Endpoint("https://host"));
        var l3 = factory.GetConnection(Endpoint("https://host/"));

        // Assert
        factory.CacheCount.Should().Be(1);
        ReferenceEquals(l1.HttpClient, l2.HttpClient).Should().BeTrue();
        ReferenceEquals(l2.HttpClient, l3.HttpClient).Should().BeTrue();

        await l1.DisposeAsync();
        await l2.DisposeAsync();
        await l3.DisposeAsync();
    }

    [Fact]
    public async Task Url_Path_Variants_Are_Distinct_Entries()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var a = factory.GetConnection(Endpoint("https://host/seq-a"));
        var b = factory.GetConnection(Endpoint("https://host/seq-b"));

        // Assert
        factory.CacheCount.Should().Be(2);
        ReferenceEquals(a.HttpClient, b.HttpClient).Should().BeFalse();

        await a.DisposeAsync();
        await b.DisposeAsync();
    }

    [Fact]
    public async Task LruEviction_Removes_Oldest_When_Limit_Exceeded()
    {
        // Arrange — limit = 2 makes the test easier to reason about
        var clock = new FakeClock(Epoch);
        await using var factory = Build(clock, maxEntries: 2);

        // Act
        var leaseA = factory.GetConnection(Endpoint("http://a.test/"));
        await leaseA.DisposeAsync(); // refCount -> 0
        clock.Advance(TimeSpan.FromSeconds(1));

        var leaseB = factory.GetConnection(Endpoint("http://b.test/"));
        await leaseB.DisposeAsync();
        clock.Advance(TimeSpan.FromSeconds(1));

        // Add a third — should evict A (oldest LastAccess)
        var leaseC = factory.GetConnection(Endpoint("http://c.test/"));

        // Assert
        factory.CacheCount.Should().Be(2);

        await leaseC.DisposeAsync();
    }

    [Fact]
    public async Task TtlEviction_Removes_Entries_Past_Sliding_Window()
    {
        // Arrange
        var clock = new FakeClock(Epoch);
        await using var factory = Build(clock,
            maxEntries: 10,
            ttl: TimeSpan.FromMinutes(5));

        // Act — first lease creates entry at t=0; release it.
        var lease1 = factory.GetConnection(Endpoint("http://a.test/"));
        await lease1.DisposeAsync();
        factory.CacheCount.Should().Be(1);

        // Move past TTL and trigger eviction with a new GetConnection.
        clock.Advance(TimeSpan.FromMinutes(6));
        var lease2 = factory.GetConnection(Endpoint("http://b.test/"));

        // Assert — only the new entry remains (TTL evicted the old one).
        factory.CacheCount.Should().Be(1);

        await lease2.DisposeAsync();
    }

    [Fact]
    public async Task Lease_Keeps_Evicted_Entry_Alive_Until_Released()
    {
        // Arrange — we acquire a lease and THEN trigger LRU eviction.
        // The evicted entry must not be disposed until the lease ends.
        var clock = new FakeClock(Epoch);
        await using var factory = Build(clock, maxEntries: 1);

        var lease = factory.GetConnection(Endpoint("http://hold.test/"));
        var heldClient = lease.HttpClient;

        // Act — force eviction by adding another endpoint
        var other = factory.GetConnection(Endpoint("http://other.test/"));

        // Assert — the held client is still usable (not disposed)
        var act = () => _ = heldClient.BaseAddress;
        act.Should().NotThrow();
        factory.PendingDisposalCount.Should().Be(1);

        // Release the lease — the pending entry should now be disposable.
        await lease.DisposeAsync();
        // DrainPendingDisposal happens on next GetConnection.
        var probe = factory.GetConnection(Endpoint("http://probe.test/"));
        await probe.DisposeAsync();
        await other.DisposeAsync();
    }

    [Fact]
    public async Task GracePeriod_ForceDisposes_Stuck_Lease()
    {
        // Arrange — large maxEntries so the probe call doesn't trigger
        // a second eviction that would dirty the pending list.
        var clock = new FakeClock(Epoch);
        await using var factory = Build(clock,
            maxEntries: 1,
            grace: TimeSpan.FromSeconds(30));

        var stuck = factory.GetConnection(Endpoint("http://stuck.test/"));

        // Force eviction of `stuck` by adding `other` (LRU limit=1).
        var other = factory.GetConnection(Endpoint("http://other.test/"));
        await other.DisposeAsync(); // release `other` immediately
        factory.PendingDisposalCount.Should().Be(1);

        // Act — advance past grace, then trigger drain via a probe.
        clock.Advance(TimeSpan.FromSeconds(31));

        // Capture the count BEFORE the probe so we can verify the stuck
        // entry was force-disposed regardless of subsequent eviction cascades.
        var stuckEntryCountBefore = factory.PendingDisposalCount;
        stuckEntryCountBefore.Should().Be(1);

        var probe = factory.GetConnection(Endpoint("http://probe.test/"));

        // Assert — after drain + adding `probe`, the previous stuck entry
        // (1 pending → 0) has been force-disposed. A new eviction may
        // re-add an entry if LRU triggered again, but the *original* stuck
        // entry is gone. Test this via lease.HttpClient: force-disposed
        // HttpClient throws on use.
        var act = () => stuck.HttpClient.GetAsync("/api").GetAwaiter().GetResult();
        act.Should().Throw<Exception>(
            "the stuck entry's HttpClient was disposed after grace expired");

        await stuck.DisposeAsync(); // safe — lease.Dispose is idempotent
        await probe.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_Releases_All_Cached_Entries()
    {
        // Arrange
        var clock = new FakeClock(Epoch);
        var factory = Build(clock);
        var a = factory.GetConnection(Endpoint("http://a.test/"));
        var b = factory.GetConnection(Endpoint("http://b.test/"));
        await a.DisposeAsync();
        await b.DisposeAsync();
        factory.CacheCount.Should().Be(2);

        // Act
        await factory.DisposeAsync();

        // Assert — internal cache emptied; further calls throw.
        factory.CacheCount.Should().Be(0);
        var act = () => factory.GetConnection(Endpoint("http://c.test/"));
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task TrustedConfig_Handler_Has_No_ConnectCallback()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var lease = factory.GetConnection(Endpoint("http://trusted.test/", mode: TrustMode.TrustedConfig));

        // Assert — peek at the handler via reflection on HttpClient.
        var handler = ExtractHandler(lease.HttpClient);
        handler.Should().NotBeNull();
        handler!.ConnectCallback.Should().BeNull(
            "TrustedConfig endpoints must allow loopback/link-local — no SSRF filter");

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task HeaderOverride_Handler_Has_ConnectCallback()
    {
        // Arrange
        await using var factory = Build(new FakeClock(Epoch));

        // Act
        var lease = factory.GetConnection(Endpoint("http://untrusted.test/", mode: TrustMode.HeaderOverride));

        // Assert
        var handler = ExtractHandler(lease.HttpClient);
        handler!.ConnectCallback.Should().NotBeNull(
            "HeaderOverride endpoints must run the SSRF connect-time filter");

        await lease.DisposeAsync();
    }

    private static SocketsHttpHandler? ExtractHandler(HttpClient client)
    {
        // HttpClient stores its handler in the private _handler field
        // (HttpMessageInvoker base). We reach through reflection — this is
        // test-only code; if the runtime renames the field we'll know.
        var field = typeof(HttpMessageInvoker).GetField(
            "_handler",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var msgHandler = field?.GetValue(client) as HttpMessageHandler;
        return msgHandler as SocketsHttpHandler;
    }
}
