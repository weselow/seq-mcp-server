using FluentAssertions;
using Moq;
using SeqMcp.Services;
using SeqMcp.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace SeqMcp.Tests.Services;

public class SeqApiClientTests
{
    private readonly SeqServerConfig _config;

    public SeqApiClientTests()
    {
        _config = new SeqServerConfig("http://localhost:5341", "test-api-key");
    }

    [Fact]
    public void Should_Create_Client_With_Valid_Config()
    {
        // Act
        var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Should_Throw_When_Config_Is_Null()
    {
        // Act
        var act = () => new SeqApiClient(null!, NullLogger<SeqApiClient>.Instance);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Should_SearchEvents_Throw_When_Filter_Is_Null()
    {
        // Arrange
        using var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync(null!, limit: 10);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Should_SearchEvents_Throw_When_Limit_Is_Invalid()
    {
        // Arrange
        using var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync("", limit: -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
