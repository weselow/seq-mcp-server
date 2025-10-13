using FluentAssertions;
using SeqMcp.Services;
using SeqMcp.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using Moq;
using Moq.Protected;

namespace SeqMcp.Tests.Services;

public class HealthCheckServiceTests
{
    [Fact]
    public async Task Should_Return_Healthy_Status_When_Seq_Is_Available()
    {
        // Arrange
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

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
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.ServiceUnavailable,
                Content = new StringContent("Service unavailable")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

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
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

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
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

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
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

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
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object)
        {
            BaseAddress = new Uri(config.ServerUrl)
        };

        var service = new HealthCheckService(
            httpClient,
            config,
            NullLogger<HealthCheckService>.Instance);

        // Act
        var health = await service.GetHealthAsync();

        // Assert
        health.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Should_Throw_ArgumentNullException_When_HttpClient_Is_Null()
    {
        // Arrange
        var config = new SeqServerConfig("http://localhost:5341", "test-api-key");

        // Act & Assert
        var act = () => new HealthCheckService(
            null!,
            config,
            NullLogger<HealthCheckService>.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    [Fact]
    public void Should_Throw_ArgumentNullException_When_Config_Is_Null()
    {
        // Arrange
        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5341") };

        // Act & Assert
        var act = () => new HealthCheckService(
            httpClient,
            null!,
            NullLogger<HealthCheckService>.Instance);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }
}
