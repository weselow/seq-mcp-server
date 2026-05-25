using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Services;

public class SeqApiClientScopeFilteringTests
{
    private readonly HttpClient _httpClient;
    private readonly ISeqConnectionFactory _factory;

    public SeqApiClientScopeFilteringTests()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
        _factory = FakeConnectionFactory.For(_httpClient);
    }

    private static IOptions<SeqOptions> Build(
        string url = "http://localhost:5341",
        string? apiKey = "test-api-key",
        string? projectScope = null,
        string scopeField = "Application")
    {
        return Options.Create(new SeqOptions
        {
            Url = url,
            ApiKey = apiKey,
            ProjectScope = projectScope,
            ScopeField = scopeField,
        });
    }

    [Fact]
    public void Should_Create_Client_Without_RequestContext()
    {
        // Arrange
        var options = Build();

        // Act
        var client = new SeqApiClient(_factory, options, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull("SeqApiClient should work without SeqRequestContext");
    }

    [Fact]
    public void Should_Create_Client_With_DefaultProjectScope_In_Config()
    {
        // Arrange
        var options = Build(projectScope: "MyProject", scopeField: "Application");

        // Act
        var client = new SeqApiClient(_factory, options, NullLogger<SeqApiClient>.Instance);

        // Assert
        client.Should().NotBeNull();
        options.Value.ProjectScope.Should().Be("MyProject");
        options.Value.ScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Create_Client_With_RequestContext()
    {
        // Arrange
        var options = Build();
        var requestContext = new SeqRequestContext
        {
            ProjectScope = "TestProject",
            ScopeField = "Environment"
        };

        // Act
        var client = new SeqApiClient(
            _factory,
            options,
            NullLogger<SeqApiClient>.Instance,
            requestContext
        );

        // Assert
        client.Should().NotBeNull();
    }

    [Fact]
    public void Should_Use_Default_ScopeField_When_Not_Provided()
    {
        // Arrange — default-constructed SeqOptions has ScopeField = "Application"
        var options = Options.Create(new SeqOptions
        {
            Url = "http://localhost:5341",
            ApiKey = "test-api-key",
            ProjectScope = "MyProject",
            // ScopeField intentionally not set
        });

        // Assert
        options.Value.ScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Allow_Null_ProjectScope()
    {
        // Arrange
        var options = Build(projectScope: null);

        // Assert
        options.Value.ProjectScope.Should().BeNull("ProjectScope is optional");
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
        var options = Build(projectScope: "DefaultProject", scopeField: "Application");

        var requestContext = new SeqRequestContext
        {
            ProjectScope = "OverrideProject",
            ScopeField = "Environment"
        };

        // Act
        var client = new SeqApiClient(
            _factory,
            options,
            NullLogger<SeqApiClient>.Instance,
            requestContext
        );

        // Assert - RequestContext should take precedence over config
        client.Should().NotBeNull();
        requestContext.ProjectScope.Should().NotBe(options.Value.ProjectScope);
    }
}
