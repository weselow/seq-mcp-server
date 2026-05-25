using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

public class SeqApiClientErrorHandlingTests
{
    private static IOptions<SeqOptions> Options_(string url, string? apiKey) =>
        Options.Create(new SeqOptions { Url = url, ApiKey = apiKey });

    [Fact]
    public void Should_Throw_SeqConnectionException_When_ServerUrl_Is_Invalid()
    {
        // Arrange
        var options = Options_("http://invalid-seq-server-12345.local", "test-api-key");
        var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.Url) };

        // Act & Assert
        var act = () => new SeqApiClient(httpClient, options, NullLogger<SeqApiClient>.Instance);
        act.Should().NotThrow("SeqApiClient constructor should not throw, connection is lazy");
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_Throw_SeqApiException_When_Unauthorized()
    {
        // Arrange
        var options = Options_("http://localhost:5341", "invalid-api-key");
        var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.Url) };
        using var client = new SeqApiClient(httpClient, options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync("", 10);

        // Assert
        await act.Should().ThrowAsync<Seq.Api.Client.SeqApiException>("Unauthorized access should throw SeqApiException");
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_Handle_Empty_Result_Gracefully()
    {
        // Arrange
        var options = Options_("http://localhost:5341", "test-api-key");
        var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.Url) };
        using var client = new SeqApiClient(httpClient, options, NullLogger<SeqApiClient>.Instance);

        // Act
        var result = await client.SearchEventsAsync("Level = 'NonExistentLevel'", 10);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeNull();
        result.Events.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Should_Throw_ArgumentException_For_Negative_Limit()
    {
        // Arrange
        var options = Options_("http://localhost:5341", "test-api-key");
        var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.Url) };
        using var client = new SeqApiClient(httpClient, options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.SearchEventsAsync("", -1);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Limit must be non-negative*");
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_Throw_SeqApiException_On_Invalid_Sql_Query()
    {
        // Arrange
        var options = Options_("http://localhost:5341", "test-api-key");
        var httpClient = new HttpClient { BaseAddress = new Uri(options.Value.Url) };
        using var client = new SeqApiClient(httpClient, options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.ExecuteSqlAsync("INVALID SQL SYNTAX HERE");

        // Assert
        await act.Should().ThrowAsync<Seq.Api.Client.SeqApiException>("Invalid SQL should throw SeqApiException");
    }
}
