using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Models;

namespace SeqMcp.Core.Services;

public interface IHealthCheckService
{
    Task<HealthCheckResponse> GetHealthAsync();
}

/// <summary>
/// Pings the configured Seq endpoint to determine service health.
/// Borrows an <see cref="HttpClient"/> from <see cref="ISeqConnectionFactory"/>
/// for the duration of each call — no separate client is created here.
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ISeqConnectionFactory _factory;
    private readonly SeqOptions _options;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly DateTime _startTime;
    private long _totalRequests;

    public HealthCheckService(
        ISeqConnectionFactory factory,
        IOptions<SeqOptions> options,
        ILogger<HealthCheckService> logger)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _startTime = DateTime.UtcNow;
        _totalRequests = 0;
    }

    public async Task<HealthCheckResponse> GetHealthAsync()
    {
        Interlocked.Increment(ref _totalRequests);

        var uptimeSeconds = (long)(DateTime.UtcNow - _startTime).TotalSeconds;
        var seqHealth = await CheckSeqConnectionAsync();

        var status = seqHealth.IsHealthy ? "healthy" : "unhealthy";
        var version = typeof(HealthCheckService).Assembly.GetName().Version?.ToString() ?? "unknown";

        var metrics = new Dictionary<string, object>
        {
            { "total_requests", _totalRequests },
            { "uptime_seconds", uptimeSeconds },
            { "seq_response_time_ms", seqHealth.ResponseTimeMs }
        };

        return new HealthCheckResponse(
            Status: status,
            Version: version,
            UptimeSeconds: uptimeSeconds,
            SeqConnection: seqHealth,
            Metrics: metrics);
    }

    private async Task<SeqHealthStatus> CheckSeqConnectionAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoint = new SeqEndpoint(_options.Url, _options.ApiKey, TrustMode.TrustedConfig);

        try
        {
            _logger.LogDebug("Checking Seq server connection at {SeqUrl}", _options.Url);

            await using var lease = _factory.GetConnection(endpoint);
            var response = await lease.HttpClient.GetAsync("/api");
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Seq server is healthy (response time: {ResponseTime}ms)",
                    stopwatch.ElapsedMilliseconds);
                return new SeqHealthStatus(
                    IsHealthy: true,
                    Message: "Connected to Seq server",
                    ResponseTimeMs: stopwatch.ElapsedMilliseconds);
            }

            _logger.LogWarning(
                "Seq server returned non-success status: {StatusCode}",
                response.StatusCode);
            return new SeqHealthStatus(
                IsHealthy: false,
                Message: $"Seq server returned status {response.StatusCode}",
                ResponseTimeMs: stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex,
                "Failed to connect to Seq server at {SeqUrl}", _options.Url);
            return new SeqHealthStatus(
                IsHealthy: false,
                Message: $"Connection failed: {ex.Message}",
                ResponseTimeMs: stopwatch.ElapsedMilliseconds);
        }
    }
}
