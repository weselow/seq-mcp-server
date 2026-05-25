using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

public class SeqApiClientSignalsTests
{
    private readonly IOptions<SeqOptions> _options;
    private readonly HttpClient _httpClient;

    public SeqApiClientSignalsTests()
    {
        _options = Options.Create(new SeqOptions
        {
            Url = "http://localhost:5341",
            ApiKey = "test-api-key",
        });
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
    }

    [Fact]
    public void Should_Have_ListSignalsAsync_Method()
    {
        // Arrange
        using var client = new SeqApiClient(_httpClient, _options, NullLogger<SeqApiClient>.Instance);

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
        using var client = new SeqApiClient(_httpClient, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var result = await client.ListSignalsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Signals.Should().NotBeNull();
        result.TotalCount.Should().BeGreaterThanOrEqualTo(0);
    }
}
