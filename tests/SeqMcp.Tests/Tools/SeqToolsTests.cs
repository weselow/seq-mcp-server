using FluentAssertions;
using Moq;
using SeqMcp.Tools;
using SeqMcp.Services;
using SeqMcp.Models;

namespace SeqMcp.Tests.Tools;

public class SeqToolsTests
{
    [Fact]
    public async Task Should_SearchEvents_Return_Serialized_Result()
    {
        // Arrange
        var mockClient = new Mock<ISeqApiClient>();
        var expectedResult = new SearchEventsResult(
            new List<SeqEvent>
            {
                new SeqEvent("1", "2025-10-12", "Information", "Test message", null)
            },
            1);

        mockClient
            .Setup(c => c.SearchEventsAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(expectedResult);

        var tools = new SeqTools(mockClient.Object);

        // Act
        var result = await tools.SearchEvents("", 10);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Test message");
        mockClient.Verify(c => c.SearchEventsAsync("", 10), Times.Once);
    }

    [Fact]
    public async Task Should_ListSignals_Return_Serialized_Result()
    {
        // Arrange
        var mockClient = new Mock<ISeqApiClient>();
        var expectedResult = new ListSignalsResult(
            new List<SeqSignal>
            {
                new SeqSignal("signal-1", "Test Signal", "Description", "Level = 'Error'")
            },
            1);

        mockClient
            .Setup(c => c.ListSignalsAsync())
            .ReturnsAsync(expectedResult);

        var tools = new SeqTools(mockClient.Object);

        // Act
        var result = await tools.ListSignals();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("Test Signal");
        mockClient.Verify(c => c.ListSignalsAsync(), Times.Once);
    }

    [Fact]
    public async Task Should_ExecuteSql_Return_Serialized_Result()
    {
        // Arrange
        var mockClient = new Mock<ISeqApiClient>();
        var expectedResult = new ExecuteSqlResult(
            "select count(*) from stream",
            "{\"count\": 42}",
            1);

        mockClient
            .Setup(c => c.ExecuteSqlAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedResult);

        var tools = new SeqTools(mockClient.Object);

        // Act
        var result = await tools.ExecuteSql("select count(*) from stream");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("count");
        mockClient.Verify(c => c.ExecuteSqlAsync("select count(*) from stream"), Times.Once);
    }
}
