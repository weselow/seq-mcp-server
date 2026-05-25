namespace SeqMcp.Core.Models;

public record HealthCheckResponse(
    string Status,
    string Version,
    long UptimeSeconds,
    SeqHealthStatus SeqConnection,
    Dictionary<string, object> Metrics
);

public record SeqHealthStatus(
    bool IsHealthy,
    string? Message,
    long ResponseTimeMs
);
