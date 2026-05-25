using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Services;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Integration tests for signal management operations.
/// Requires a running Seq server at http://localhost:5341
/// </summary>
public class SeqApiClientSignalManagementIntegrationTests : IAsyncLifetime
{
    private readonly IOptions<SeqOptions> _options;
    private readonly HttpClient _httpClient;
    private readonly ISeqConnectionFactory _factory;
    private string? _createdSignalId;

    public SeqApiClientSignalManagementIntegrationTests()
    {
        _options = Options.Create(new SeqOptions { Url = "http://localhost:5341", ApiKey = null });
        _httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };
        _factory = FakeConnectionFactory.For(_httpClient);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Cleanup: Delete created signal if exists
        if (!string.IsNullOrEmpty(_createdSignalId))
        {
            try
            {
                var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
                await client.DeleteSignalAsync(_createdSignalId);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_CreateSignal_Successfully()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var title = $"Test Signal {Guid.NewGuid():N}";
        var description = "Integration test signal";
        var filter = "Level = 'Error'";

        // Act
        var result = await client.CreateSignalAsync(title, description, filter);

        // Assert
        result.Should().NotBeNull();
        result.SignalId.Should().NotBeNullOrEmpty();
        result.Title.Should().Be(title);
        result.Message.Should().Contain("created successfully");

        // Save for cleanup
        _createdSignalId = result.SignalId;
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_CreateSignal_WithoutFilter()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var title = $"Test Signal No Filter {Guid.NewGuid():N}";

        // Act
        var result = await client.CreateSignalAsync(title, null, null);

        // Assert
        result.Should().NotBeNull();
        result.SignalId.Should().NotBeNullOrEmpty();
        result.Title.Should().Be(title);

        // Save for cleanup
        _createdSignalId = result.SignalId;
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_UpdateSignal_Successfully()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var originalTitle = $"Original Signal {Guid.NewGuid():N}";
        var createResult = await client.CreateSignalAsync(originalTitle, "Original desc", "Level = 'Error'");
        _createdSignalId = createResult.SignalId;

        var newTitle = $"Updated Signal {Guid.NewGuid():N}";
        var newDescription = "Updated description";
        var newFilter = "Level = 'Warning'";

        // Act
        var updateResult = await client.UpdateSignalAsync(_createdSignalId, newTitle, newDescription, newFilter);

        // Assert
        updateResult.Should().NotBeNull();
        updateResult.SignalId.Should().Be(_createdSignalId);
        updateResult.Message.Should().Contain("updated successfully");

        // Verify the update by listing signals
        var listResult = await client.ListSignalsAsync();
        var updatedSignal = listResult.Signals.FirstOrDefault(s => s.Id == _createdSignalId);
        updatedSignal.Should().NotBeNull();
        updatedSignal!.Title.Should().Be(newTitle);
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_UpdateSignal_PartialUpdate()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var originalTitle = $"Partial Update Test {Guid.NewGuid():N}";
        var createResult = await client.CreateSignalAsync(originalTitle, "Original", "Level = 'Error'");
        _createdSignalId = createResult.SignalId;

        var newTitle = $"New Title {Guid.NewGuid():N}";

        // Act - Update only title
        var updateResult = await client.UpdateSignalAsync(_createdSignalId, title: newTitle);

        // Assert
        updateResult.Should().NotBeNull();
        updateResult.SignalId.Should().Be(_createdSignalId);
        updateResult.Message.Should().Contain("updated successfully");
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_DeleteSignal_Successfully()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var title = $"Signal To Delete {Guid.NewGuid():N}";
        var createResult = await client.CreateSignalAsync(title, "Will be deleted", null);
        var signalId = createResult.SignalId;

        // Act
        var deleteResult = await client.DeleteSignalAsync(signalId);

        // Assert
        deleteResult.Should().NotBeNull();
        deleteResult.SignalId.Should().Be(signalId);
        deleteResult.Message.Should().Contain("deleted successfully");

        // Verify deletion by trying to list signals
        var listResult = await client.ListSignalsAsync();
        var deletedSignal = listResult.Signals.FirstOrDefault(s => s.Id == signalId);
        deletedSignal.Should().BeNull("signal should be deleted");

        // Clear cleanup ID since we already deleted it
        _createdSignalId = null;
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_GetApplications_Successfully()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);

        // Act
        var result = await client.GetApplicationsAsync(limit: 10);

        // Assert
        result.Should().NotBeNull();
        result.Applications.Should().NotBeNull();
        result.TotalCount.Should().Be(result.Applications.Count);

        // If there are applications, verify structure
        if (result.Applications.Count > 0)
        {
            var firstApp = result.Applications.First();
            firstApp.Name.Should().NotBeNullOrEmpty();
            firstApp.EventCount.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_GetApplications_RespectLimit()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var limit = 5;

        // Act
        var result = await client.GetApplicationsAsync(limit);

        // Assert
        result.Should().NotBeNull();
        result.Applications.Should().NotBeNull();
        result.Applications.Count.Should().BeLessThanOrEqualTo(limit);
    }

    [Fact(Skip = "Requires running Seq server at http://localhost:5341")]
    public async Task Should_CreateUpdateDelete_FullLifecycle()
    {
        // Arrange
        var client = new SeqApiClient(_factory, _options, NullLogger<SeqApiClient>.Instance);
        var originalTitle = $"Lifecycle Test {Guid.NewGuid():N}";

        // Act & Assert - Create
        var createResult = await client.CreateSignalAsync(
            originalTitle,
            "Lifecycle test signal",
            "Level = 'Error'",
            isProtected: false
        );
        createResult.Should().NotBeNull();
        createResult.SignalId.Should().NotBeNullOrEmpty();
        _createdSignalId = createResult.SignalId;

        // Act & Assert - Update
        var updatedTitle = $"Updated Lifecycle {Guid.NewGuid():N}";
        var updateResult = await client.UpdateSignalAsync(
            _createdSignalId,
            title: updatedTitle,
            description: "Updated description",
            filter: "Level = 'Warning'"
        );
        updateResult.Should().NotBeNull();
        updateResult.Message.Should().Contain("updated successfully");

        // Verify update
        var listAfterUpdate = await client.ListSignalsAsync();
        var updatedSignal = listAfterUpdate.Signals.FirstOrDefault(s => s.Id == _createdSignalId);
        updatedSignal.Should().NotBeNull();
        updatedSignal!.Title.Should().Be(updatedTitle);

        // Act & Assert - Delete
        var deleteResult = await client.DeleteSignalAsync(_createdSignalId);
        deleteResult.Should().NotBeNull();
        deleteResult.Message.Should().Contain("deleted successfully");

        // Verify deletion
        var listAfterDelete = await client.ListSignalsAsync();
        var deletedSignal = listAfterDelete.Signals.FirstOrDefault(s => s.Id == _createdSignalId);
        deletedSignal.Should().BeNull();

        // Clear cleanup ID
        _createdSignalId = null;
    }
}
