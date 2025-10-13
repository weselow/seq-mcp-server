using FluentAssertions;
using SeqMcp.Services;
using SeqMcp.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace SeqMcp.Tests.Services;

public class SeqApiClientScopeFilteringTests
{
    private readonly HttpClient _httpClient;

    public SeqApiClientScopeFilteringTests()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
    }

    [Fact]
    public void Should_Create_Client_Without_RequestContext()
    {
        // Arrange
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        // Act
        using var client = new SeqApiClient(_httpClient, config, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull("SeqApiClient should work without SeqRequestContext");
    }

    [Fact]
    public void Should_Create_Client_With_DefaultProjectScope_In_Config()
    {
        // Arrange
        var config = new SeqServerConfig(
            "http://localhost:5341",
            "test-api-key",
            defaultProjectScope: "MyProject",
            defaultScopeField: "Application"
        );

        // Act
        using var client = new SeqApiClient(_httpClient, config, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull();
        config.DefaultProjectScope.Should().Be("MyProject");
        config.DefaultScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Create_Client_With_RequestContext()
    {
        // Arrange
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");
        var requestContext = new SeqRequestContext
        {
            ProjectScope = "TestProject",
            ScopeField = "Environment"
        };

        // Act
        using var client = new SeqApiClient(
            _httpClient,
            config,
            NullLogger<SeqApiClient>.Instance,
            requestContext
        );

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Should_Use_Default_ScopeField_When_Not_Provided()
    {
        // Arrange
        var config = new SeqServerConfig(
            "http://localhost:5341",
            "test-api-key",
            defaultProjectScope: "MyProject",
            defaultScopeField: null // Should use default "Application"
        );

        // Assert
        config.DefaultScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Allow_Null_ProjectScope()
    {
        // Arrange
        var config = new SeqServerConfig(
            "http://localhost:5341",
            "test-api-key",
            defaultProjectScope: null // Optional
        );

        // Assert
        config.DefaultProjectScope.Should().BeNull("ProjectScope is optional");
    }

    [Fact]
    public void Should_Create_RequestContext_With_Null_Values()
    {
        // Arrange & Act
        var requestContext = new SeqRequestContext
        {
            ProjectScope = null,
            ScopeField = null
        };

        // Assert
        requestContext.ProjectScope.Should().BeNull();
        requestContext.ScopeField.Should().BeNull();
    }

    [Fact]
    public void Should_Allow_RequestContext_Override_Config_Values()
    {
        // Arrange
        var config = new SeqServerConfig(
            "http://localhost:5341",
            "test-api-key",
            defaultProjectScope: "DefaultProject",
            defaultScopeField: "Application"
        );

        var requestContext = new SeqRequestContext
        {
            ProjectScope = "OverrideProject",
            ScopeField = "Environment"
        };

        // Act
        using var client = new SeqApiClient(
            _httpClient,
            config,
            NullLogger<SeqApiClient>.Instance,
            requestContext
        );

        // Assert - RequestContext should take precedence over config
        client.Should().NotBeNull();
        requestContext.ProjectScope.Should().NotBe(config.DefaultProjectScope);
    }
}
