using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Hosting;
using SeqMcp.Http.Middleware;
using SeqMcp.Core.Services;
using SeqMcp.Core.Tools;
using SeqMcp.Core.Resources;
using SeqMcp.Core.Prompts;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Register Seq options with the legacy per-field priority
// (env vs appsettings) — see SeqOptionsLoader for the table.
builder.Services.AddSeqOptions(builder.Configuration);

// Configure server port
// Priority: appsettings.json > Environment Variable
var serverPort = builder.Configuration["McpServer:Port"]
    ?? Environment.GetEnvironmentVariable("PORT")
    ?? "5555";

// Bind to 0.0.0.0 to accept external connections (required for Docker)
var serverUrl = $"http://0.0.0.0:{serverPort}";

// Configure server URLs (MUST be before Build())
builder.WebHost.UseUrls(serverUrl);

// Register Seq services
builder.Services.AddScoped<SeqRequestContext>(); // Per-request context for HTTP headers

// Connection factory: owns every HttpClient + SeqConnection + SocketsHttpHandler
// in the process. Singleton; DI disposes it on shutdown which tears down all
// cached client pairs (it implements IAsyncDisposable).
builder.Services.AddSingleton<ISeqConnectionFactory, SeqConnectionFactory>();

builder.Services.AddScoped<ISeqApiClient, SeqApiClient>();
builder.Services.AddScoped<SeqTools>();
builder.Services.AddScoped<SeqResources>();
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();

// Register MCP server with HTTP transport, tools, resources, and prompts
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<SeqTools>()
    .WithResources<SeqResources>()
    .WithPrompts<SeqPrompts>();

var app = builder.Build();

// Extract Seq scope configuration from HTTP headers (if provided)
app.Use(async (context, next) =>
{
    var requestContext = context.RequestServices.GetRequiredService<SeqRequestContext>();

    // Extract X-Seq-Project-Scope header (optional)
    if (context.Request.Headers.TryGetValue("X-Seq-Project-Scope", out var projectScope))
    {
        requestContext.ProjectScope = projectScope.ToString();
    }

    // Extract X-Seq-Scope-Field header (optional)
    if (context.Request.Headers.TryGetValue("X-Seq-Scope-Field", out var scopeField))
    {
        requestContext.ScopeField = scopeField.ToString();
    }

    await next();
});

// HTTP request logging: только метаданные на Debug-уровне.
// Никакого буферирования response (ломает SSE), никакого логирования тел и заголовков (утечка ключей).
app.UseMiddleware<RequestLoggingMiddleware>();

// Map Health Check endpoint
app.MapGet("/health", async (IHealthCheckService healthCheckService) =>
{
    var health = await healthCheckService.GetHealthAsync();

    var statusCode = health.Status == "healthy"
        ? Results.Ok(health)
        : Results.Json(health, statusCode: 503);

    return statusCode;
});

// Map MCP endpoints
app.MapMcp();

var startupOptions = app.Services.GetRequiredService<IOptions<SeqOptions>>().Value;
app.Logger.LogInformation("Seq MCP Server starting...");
app.Logger.LogInformation("Server URL: {ServerUrl}", serverUrl);
app.Logger.LogInformation("Seq URL: {SeqUrl}", startupOptions.Url);
app.Logger.LogInformation("Seq API Key: {ApiKeyStatus}",
    string.IsNullOrEmpty(startupOptions.ApiKey) ? "NOT SET" : $"SET (length: {startupOptions.ApiKey.Length})");
app.Logger.LogInformation("Transport: HTTP/SSE");

// Log all registered endpoints
var endpoints = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>()
    .Endpoints
    .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>();

app.Logger.LogInformation("Registered MCP endpoints:");
foreach (var endpoint in endpoints)
{
    app.Logger.LogInformation("  - {Pattern} ({Methods})",
        endpoint.RoutePattern.RawText,
        string.Join(", ", endpoint.Metadata.OfType<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()
            .SelectMany(m => m.HttpMethods)));
}

app.Logger.LogInformation("MCP Tools: 7 (search, signals, sql, create_signal, update_signal, delete_signal, get_apps)");
app.Logger.LogInformation("MCP Resources: 9 (events, errors, warnings, signals, exceptions, last-hour, today, slow, summary)");
app.Logger.LogInformation("MCP Prompts: 8 (анализ ошибок, исключения, активность, безопасность и др.)");

await app.RunAsync();
