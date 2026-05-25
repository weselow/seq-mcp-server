using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

public class SeqApiClientTests
{
    private readonly IOptions<SeqOptions> _options;
    private readonly HttpClient _httpClient;

    public SeqApiClientTests()
    {
        _options = Options.Create(new SeqOptions
        {
            Url = "http://localhost:5341",
            ApiKey = "test-api-key",
        });
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
    }

    [Fact]
    public void Should_Create_Client_With_Valid_Config()
    {
        // Act
        var client = new SeqApiClient(_httpClient, _options, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Should_Throw_When_HttpClient_Is_Null()
    {
        // Act
        var act = () => new SeqApiClient(null!, _options, NullLogger<SeqApiClient>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Should_Throw_When_Config_Is_Null()
    {
        // Act
        var act = () => new SeqApiClient(_httpClient, null!, NullLogger<SeqApiClient>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_SearchEvents_Throw_When_Filter_Is_Null()
    {
        // Arrange
        using var client = new SeqApiClient(_httpClient, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync(null!, limit: 10);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Should_SearchEvents_Throw_When_Limit_Is_Invalid()
    {
        // Arrange
        using var client = new SeqApiClient(_httpClient, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync("", limit: -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
