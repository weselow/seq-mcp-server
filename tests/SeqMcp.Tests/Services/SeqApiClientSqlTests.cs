using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Services;

public class SeqApiClientSqlTests
{
    private readonly IOptions<SeqOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly ISeqConnectionFactory _factory;

    public SeqApiClientSqlTests()
    {
        _options = Options.Create(new SeqOptions
        {
            Url = "http://localhost:5341",
            ApiKey = "test-api-key",
        });
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
        _factory = FakeConnectionFactory.For(_httpClient);
    }

    [Fact]
    public async Task Should_ExecuteSql_Throw_When_Query_Is_Null()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var act = async () => await client.ExecuteSqlAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void Should_Have_ExecuteSqlAsync_Method()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var method = client.GetType().GetMethod("ExecuteSqlAsync");

        // Assert
        method.Should().NotBeNull("ExecuteSqlAsync method should exist");
        method!.ReturnType.Should().Be(typeof(Task<SeqMcp.Core.Models.ExecuteSqlResult>));
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task Should_ExecuteSql_Return_Result_Integration()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var query = "select count(*) from stream";

        // Act
        var result = await client.ExecuteSqlAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Query.Should().Be(query);
        result.Result.Should().NotBeNullOrEmpty();
    }
}
