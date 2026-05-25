using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Verifies <see cref="SeqApiClient"/>'s endpoint resolution: how it
/// combines startup <see cref="SeqOptions"/> with the per-request
/// <see cref="SeqRequestContext"/> into a <see cref="SeqEndpoint"/>
/// that goes to <see cref="ISeqConnectionFactory.GetConnection"/>.
///
/// <see cref="SeqApiClient.ResolveEndpoint"/> itself is private — we
/// observe it through the endpoint captured by the factory mock when
/// any public method (here: <see cref="SeqApiClient.SearchEventsAsync"/>)
/// is invoked.
/// </summary>
public class SeqApiClientResolveEndpointTests
{
    private static IOptions<SeqOptions> BuildOptions(
        string url = "http://config.seq.local:5341",
        string? apiKey = "config-key",
        bool allowUrlOverride = false,
        bool blockPrivateHosts = false)
    {
        return Options.Create(new SeqOptions
        {
            Url = url,
            ApiKey = apiKey,
            AllowUrlOverride = allowUrlOverride,
            BlockPrivateHosts = blockPrivateHosts,
        });
    }

    private static (SeqApiClient Client, List<SeqEndpoint> Captured)
        CreateClient(IOptions<SeqOptions> options, SeqRequestContext? context = null)
    {
        var captured = new List<SeqEndpoint>();
        var handler = new StubHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri(options.Value.Url) };

        var factoryMock = new Mock<ISeqConnectionFactory>();
        factoryMock
            .Setup(f => f.GetConnection(It.IsAny<SeqEndpoint>()))
            .Returns<SeqEndpoint>(ep =>
            {
                captured.Add(ep);
                return new StubLease(http);
            });

        var client = new SeqApiClient(
            factoryMock.Object,
            options,
            NullLogger<SeqApiClient>.Instance,
            context);
        return (client, captured);
    }

    [Fact]
    public async Task Should_Use_TrustedConfig_When_RequestContext_Has_No_Url()
    {
        // Arrange
        var options = BuildOptions(url: "http://config.seq.local:5341", apiKey: "config-key");
        var (client, captured) = CreateClient(options, context: null);

        // Act
        await client.SearchEventsAsync(filter: "", limit: 1);

        // Assert
        captured.Should().ContainSingle();
        captured[0].Url.Should().Be("http://config.seq.local:5341");
        captured[0].ApiKey.Should().Be("config-key");
        captured[0].TrustMode.Should().Be(TrustMode.TrustedConfig);
    }

    [Fact]
    public async Task Should_Use_TrustedConfig_When_RequestContext_Has_Empty_Url()
    {
        // Arrange — empty context URL is treated as "not set"
        var options = BuildOptions();
        var context = new SeqRequestContext { SeqUrl = "", ApiKey = null };
        var (client, captured) = CreateClient(options, context);

        // Act
        await client.SearchEventsAsync(filter: "", limit: 1);

        // Assert
        captured[0].TrustMode.Should().Be(TrustMode.TrustedConfig);
        captured[0].Url.Should().Be("http://config.seq.local:5341");
    }

    [Fact]
    public async Task Should_Use_HeaderOverride_When_RequestContext_Has_Url()
    {
        // Arrange
        var options = BuildOptions(url: "http://config.seq.local:5341", apiKey: "config-key");
        var context = new SeqRequestContext
        {
            SeqUrl = "http://override.seq.example.com:8080",
            ApiKey = "override-key",
        };
        var (client, captured) = CreateClient(options, context);

        // Act
        await client.SearchEventsAsync(filter: "", limit: 1);

        // Assert
        captured.Should().ContainSingle();
        captured[0].Url.Should().Be("http://override.seq.example.com:8080");
        captured[0].ApiKey.Should().Be("override-key");
        captured[0].TrustMode.Should().Be(TrustMode.HeaderOverride);
    }

    [Fact]
    public async Task Should_Fallback_To_Config_ApiKey_When_Only_Url_Overridden()
    {
        // Arrange — URL from context, ApiKey absent → use config's ApiKey
        var options = BuildOptions(url: "http://config.seq.local", apiKey: "config-key");
        var context = new SeqRequestContext
        {
            SeqUrl = "http://override.seq.example.com",
            ApiKey = null,
        };
        var (client, captured) = CreateClient(options, context);

        // Act
        await client.SearchEventsAsync(filter: "", limit: 1);

        // Assert
        captured[0].Url.Should().Be("http://override.seq.example.com");
        captured[0].ApiKey.Should().Be("config-key",
            "context.ApiKey null falls back to startup options.ApiKey");
        captured[0].TrustMode.Should().Be(TrustMode.HeaderOverride);
    }

    [Fact]
    public async Task Should_Use_HeaderOverride_Regardless_Of_AllowUrlOverride_Setting()
    {
        // Arrange — middleware is the gatekeeper for AllowUrlOverride; once
        // SeqRequestContext.SeqUrl is set, SeqApiClient just trusts it.
        // (This is by design — duplicating the check would create surprising
        // double-validation behavior.)
        var options = BuildOptions(allowUrlOverride: false);
        var context = new SeqRequestContext { SeqUrl = "http://override.seq:8080" };
        var (client, captured) = CreateClient(options, context);

        // Act
        await client.SearchEventsAsync(filter: "", limit: 1);

        // Assert
        captured[0].TrustMode.Should().Be(TrustMode.HeaderOverride);
        captured[0].Url.Should().Be("http://override.seq:8080");
    }

    private sealed class StubLease : IConnectionLease
    {
        public StubLease(HttpClient httpClient)
        {
            HttpClient = httpClient;
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

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]"),
            });
        }
    }
}
