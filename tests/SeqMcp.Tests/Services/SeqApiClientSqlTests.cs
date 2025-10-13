using FluentAssertions;
using SeqMcp.Services;
using SeqMcp.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace SeqMcp.Tests.Services;

public class SeqApiClientSqlTests
{
    private readonly SeqServerConfig _config;

    public SeqApiClientSqlTests()
    {
        _config = new SeqServerConfig("http://localhost:5341", "test-api-key");
    }

    [Fact]
    public async Task Should_ExecuteSql_Throw_When_Query_Is_Null()
    {
        // Arrange
        using var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.ExecuteSqlAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Should_Have_ExecuteSqlAsync_Method()
    {
        // Arrange
        using var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);

        // Act
        var method = client.GetType().GetMethod("ExecuteSqlAsync");

        // Assert
        method.Should().NotBeNull("ExecuteSqlAsync method should exist");
        method!.ReturnType.Should().Be(typeof(Task<SeqMcp.Models.ExecuteSqlResult>));
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_ExecuteSql_Return_Result_Integration()
    {
        // Arrange
        using var client = new SeqApiClient(_config, NullLogger<SeqApiClient>.Instance);
        var query = "select count(*) from stream";

        // Act
        var result = await client.ExecuteSqlAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Query.Should().Be(query);
        result.Result.Should().NotBeNullOrEmpty();
    }
}
