using FluentAssertions;
using SeqMcp.Core.Services;
using SeqMcp.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace SeqMcp.Tests.Services;

public class SeqApiClientSignalsTests
{
    private readonly SeqServerConfig _config;
    private readonly HttpClient _httpClient;

    public SeqApiClientSignalsTests()
    {
        _config = new SeqServerConfig("http://localhost:5341", "test-api-key");
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
    }

    [Fact]
    public void Should_Have_ListSignalsAsync_Method()
    {
        // Arrange
        using var client = new SeqApiClient(_httpClient, _config, NullLogger<SeqApiClient>.Instance);

        // Act
        var method = client.GetType().GetMethod("ListSignalsAsync");

        // Assert
        method.Should().NotBeNull("ListSignalsAsync method should exist");
        method!.ReturnType.Should().Be(typeof(Task<SeqMcp.Core.Models.ListSignalsResult>));
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_ListSignals_Return_Result_Integration()
    {
        // Arrange
        using var client = new SeqApiClient(_httpClient, _config, NullLogger<SeqApiClient>.Instance);

        // Act
        var result = await client.ListSignalsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Signals.Should().NotBeNull();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
