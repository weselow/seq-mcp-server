using FluentAssertions;

namespace SeqMcp.Tests.Configuration;

public class SeqServerConfigTests
{
    [Fact]
    public void Should_Create_Config_With_Valid_Url()
    {
        // Arrange
        var url = "http://localhost:5341";
        var apiKey = "test-api-key";

        // Act
        var config = new SeqMcp.Configuration.SeqServerConfig(url, apiKey);

        // Assert
        config.ServerUrl.Should().Be(url);
        config.ApiKey.Should().Be(apiKey);
    }

    [Fact]
    public void Should_Create_Config_Without_ApiKey()
    {
        // Arrange
        var url = "http://localhost:5341";

        // Act
        var config = new SeqMcp.Configuration.SeqServerConfig(url);

        // Assert
        config.ServerUrl.Should().Be(url);
        config.ApiKey.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Should_Throw_When_Url_Is_Invalid(string? invalidUrl)
    {
        // Act
        var act = () => new SeqMcp.Configuration.SeqServerConfig(invalidUrl!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*ServerUrl*");
    }

    [Fact]
    public void Should_Have_Default_EventLimit()
    {
        // Arrange
        var url = "http://localhost:5341";

        // Act
        var config = new SeqMcp.Configuration.SeqServerConfig(url);

        // Assert
        config.DefaultEventLimit.Should().Be(100);
    }
}
