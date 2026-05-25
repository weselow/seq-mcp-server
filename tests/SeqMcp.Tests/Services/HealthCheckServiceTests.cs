using FluentAssertions;
using SeqMcp.Core.Services;
using SeqMcp.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Moq;
using Moq.Protected;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Services;

public class HealthCheckServiceTests
{
    private static IOptions<SeqOptions> Options_(string url = "http://localhost:5341", string? apiKey = "test-api-key") =>
        Options.Create(new SeqOptions { Url = url, ApiKey = apiKey });

    private static (ISeqConnectionFactory factory, HttpClient client) BuildFactoryWith(Mock<HttpMessageHandler> handlerMock, string url)
    {
        var http = new HttpClient(handlerMock.Object) { BaseAddress = new Uri(url) };
        return (FakeConnectionFactory.For(http), http);
    }

    private static Mock<HttpMessageHandler> CreateOkHandler() =>
        CreateHandlerReturning(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent("{}")
        });

    private static Mock<HttpMessageHandler> CreateHandlerReturning(HttpResponseMessage response)
    {
        var mock = new Mock<HttpMessageHandler>();
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return mock;
    }

    [Fact]
    public async Task Should_Return_Healthy_Status_When_Seq_Is_Available()
    {
        // Arrange
        var options = Options_();
        var handler = CreateOkHandler();
        var (factory, _) = BuildFactoryWith(handler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health = await service.GetHealthAsync();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be("healthy");
        health.SeqConnection.IsHealthy.Should().BeTrue();
        health.SeqConnection.Message.Should().Be("Connected to Seq server");
        health.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
        health.Metrics.Should().ContainKey("total_requests");
        health.Metrics.Should().ContainKey("uptime_seconds");
        health.Metrics.Should().ContainKey("seq_response_time_ms");
    }

    [Fact]
    public async Task Should_Return_Unhealthy_Status_When_Seq_Returns_Error()
    {
        // Arrange
        var options = Options_();
        var handler = CreateHandlerReturning(new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.ServiceUnavailable,
            Content = new StringContent("Service unavailable")
        });
        var (factory, _) = BuildFactoryWith(handler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health = await service.GetHealthAsync();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be("unhealthy");
        health.SeqConnection.IsHealthy.Should().BeFalse();
        health.SeqConnection.Message.Should().Contain("ServiceUnavailable");
    }

    [Fact]
    public async Task Should_Return_Unhealthy_Status_When_Seq_Connection_Fails()
    {
        // Arrange
        var options = Options_();
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var (factory, _) = BuildFactoryWith(mockHandler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health = await service.GetHealthAsync();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be("unhealthy");
        health.SeqConnection.IsHealthy.Should().BeFalse();
        health.SeqConnection.Message.Should().Contain("Connection failed");
        health.SeqConnection.Message.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task Should_Track_Total_Requests()
    {
        // Arrange
        var options = Options_();
        var handler = CreateOkHandler();
        var (factory, _) = BuildFactoryWith(handler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health1 = await service.GetHealthAsync();
        var health2 = await service.GetHealthAsync();
        var health3 = await service.GetHealthAsync();

        // Assert
        health1.Metrics["total_requests"].Should().Be(1L);
        health2.Metrics["total_requests"].Should().Be(2L);
        health3.Metrics["total_requests"].Should().Be(3L);
    }

    [Fact]
    public async Task Should_Track_Uptime()
    {
        // Arrange
        var options = Options_();
        var handler = CreateOkHandler();
        var (factory, _) = BuildFactoryWith(handler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health1 = await service.GetHealthAsync();
        await Task.Delay(1100); // Wait more than 1 second
        var health2 = await service.GetHealthAsync();

        // Assert
        health1.UptimeSeconds.Should().BeGreaterThanOrEqualTo(0);
        health2.UptimeSeconds.Should().BeGreaterThan(health1.UptimeSeconds);
        health2.UptimeSeconds.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Should_Include_Version_In_Response()
    {
        // Arrange
        var options = Options_();
        var handler = CreateOkHandler();
        var (factory, _) = BuildFactoryWith(handler, options.Value.Url);
        var service = new HealthCheckService(factory, options, NullLogger<HealthCheckService>.Instance);

        // Act
        var health = await service.GetHealthAsync();

        // Assert
        health.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_Throw_ArgumentNullException_When_Factory_Is_Null()
    {
        // Arrange
        var options = Options_();

        // Act & Assert
        var act = () => new HealthCheckService(
            null!,
            options,
            NullLogger<HealthCheckService>.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("factory");
    }

    [Fact]
    public void Should_Throw_ArgumentNullException_When_Config_Is_Null()
    {
        // Arrange
        var handler = CreateOkHandler();
        var (factory, _) = BuildFactoryWith(handler, "http://localhost:5341");

        // Act & Assert
        var act = () => new HealthCheckService(
            factory,
            null!,
            NullLogger<HealthCheckService>.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }
}
