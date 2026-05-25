using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SeqMcp.Middleware;

/// Логирует метаданные HTTP-запросов на уровне Debug, не буферит response body,
/// не читает request body. Безопасен для SSE-стримов MCP.
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogDebug(
                "HTTP {Method} {Path} {StatusCode} from {RemoteIp} in {ElapsedMs}ms (request {RequestBytes}B)",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                context.Connection.RemoteIpAddress,
                stopwatch.ElapsedMilliseconds,
                context.Request.ContentLength ?? 0L);
        }
    }
}
