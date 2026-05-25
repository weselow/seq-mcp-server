using Moq;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Helpers;

/// <summary>
/// Test helper that exposes a mocked <see cref="ISeqConnectionFactory"/>
/// whose <c>GetConnection</c> returns a stub <see cref="IConnectionLease"/>
/// backed by the supplied <see cref="HttpClient"/>. Used by existing
/// <c>SeqApiClient</c> / <c>HealthCheckService</c> tests that previously
/// took an <c>HttpClient</c> directly.
///
/// The lease never disposes the underlying HttpClient — the test owns it.
/// </summary>
internal static class FakeConnectionFactory
{
    public static Mock<ISeqConnectionFactory> Mock(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        var mock = new Mock<ISeqConnectionFactory>();
        mock.Setup(f => f.GetConnection(It.IsAny<SeqEndpoint>()))
            .Returns(() => new FakeLease(httpClient));
        return mock;
    }

    public static ISeqConnectionFactory For(HttpClient httpClient) =>
        Mock(httpClient).Object;

    private sealed class FakeLease : IConnectionLease
    {
        public FakeLease(HttpClient httpClient)
        {
            HttpClient = httpClient;
            // SeqConnection isn't exercised by the existing tests — they only
            // touch HttpClient. We construct one to satisfy the contract but
            // never use it.
            SeqConnection = new Seq.Api.SeqConnection("http://localhost:65535");
        }

        public HttpClient HttpClient { get; }
        public Seq.Api.SeqConnection SeqConnection { get; }

        public ValueTask DisposeAsync()
        {
            SeqConnection.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
