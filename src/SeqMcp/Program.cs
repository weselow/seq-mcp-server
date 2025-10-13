using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using SeqMcp.Configuration;
using SeqMcp.Services;
using SeqMcp.Tools;
using SeqMcp.Resources;
using SeqMcp.Prompts;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Configure Seq settings
// Priority: appsettings.json > Environment Variables
var seqUrl = builder.Configuration["Seq:Url"]
    ?? Environment.GetEnvironmentVariable("SEQ_URL")
    ?? Environment.GetEnvironmentVariable("SEQ_SERVER_URL")
    ?? "http://localhost:8080";

var seqApiKey = builder.Configuration["Seq:ApiKey"]
    ?? Environment.GetEnvironmentVariable("SEQ_API_KEY");

// Optional: Project scope filtering
var defaultProjectScope = builder.Configuration["Seq:ProjectScope"]
    ?? Environment.GetEnvironmentVariable("SEQ_PROJECT_SCOPE");

var defaultScopeField = builder.Configuration["Seq:ScopeField"]
    ?? Environment.GetEnvironmentVariable("SEQ_SCOPE_FIELD");

var seqConfig = new SeqServerConfig(seqUrl, seqApiKey, defaultProjectScope, defaultScopeField);

// Configure server port
// Priority: appsettings.json > Environment Variable
var serverPort = builder.Configuration["McpServer:Port"]
    ?? Environment.GetEnvironmentVariable("PORT")
    ?? "5555";

var serverUrl = $"http://localhost:{serverPort}";

// Configure server URLs (MUST be before Build())
builder.WebHost.UseUrls(serverUrl);

// Register Seq services
builder.Services.AddSingleton(seqConfig);
builder.Services.AddScoped<SeqRequestContext>(); // Per-request context for HTTP headers

// Register optimized HttpClient as Singleton for Seq API
builder.Services.AddSingleton<HttpClient>(sp =>
{
    var config = sp.GetRequiredService<SeqServerConfig>();

    // Configure SocketsHttpHandler with production-optimized settings
    var handler = new SocketsHttpHandler
    {
        // CONNECTION LIFETIME: 5 minutes - balance between performance and DNS updates
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),

        // IDLE TIMEOUT: 2 minutes - close unused connections
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),

        // CONNECTION LIMIT: 10 concurrent connections to Seq server
        MaxConnectionsPerServer = 10,

        // CONNECT TIMEOUT: 15 seconds - local Seq should connect quickly
        ConnectTimeout = TimeSpan.FromSeconds(15),

        // DRAIN TIMEOUT: 5 seconds - time to drain response before closing
        ResponseDrainTimeout = TimeSpan.FromSeconds(5),

        // PERFORMANCE: Disable unnecessary features
        AllowAutoRedirect = false,  // Seq API doesn't use redirects
        UseCookies = false,          // Seq uses API keys, not cookies

        // COMPRESSION: Enable to reduce response sizes
        AutomaticDecompression = System.Net.DecompressionMethods.GZip
                               | System.Net.DecompressionMethods.Deflate
    };

    var client = new HttpClient(handler, disposeHandler: true)
    {
        // REQUEST TIMEOUT: 30 seconds - balance between fast queries and slow SQL
        Timeout = TimeSpan.FromSeconds(30),
        BaseAddress = new Uri(config.ServerUrl)
    };

    if (!string.IsNullOrEmpty(config.ApiKey))
    {
        client.DefaultRequestHeaders.Add("X-Seq-ApiKey", config.ApiKey);
    }

    return client;
});

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

// Enable detailed request/response logging
app.Use(async (context, next) =>
{
    app.Logger.LogInformation("HTTP {Method} {Path} from {RemoteIp}",
        context.Request.Method,
        context.Request.Path,
        context.Connection.RemoteIpAddress);

    // Read request body
    context.Request.EnableBuffering();
    using var reader = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
    var requestBody = await reader.ReadToEndAsync();
    context.Request.Body.Position = 0;

    if (!string.IsNullOrEmpty(requestBody))
    {
        app.Logger.LogInformation("Request body: {Body}", requestBody);
    }

    // Capture response
    var originalBody = context.Response.Body;
    using var responseBody = new MemoryStream();
    context.Response.Body = responseBody;

    await next();

    responseBody.Seek(0, SeekOrigin.Begin);
    var responseText = await new StreamReader(responseBody).ReadToEndAsync();
    responseBody.Seek(0, SeekOrigin.Begin);

    app.Logger.LogInformation("Response status: {Status}, body: {Body}",
        context.Response.StatusCode,
        responseText);

    await responseBody.CopyToAsync(originalBody);
});

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

app.Logger.LogInformation("Seq MCP Server starting...");
app.Logger.LogInformation("Server URL: {ServerUrl}", serverUrl);
app.Logger.LogInformation("Seq URL: {SeqUrl}", seqUrl);
app.Logger.LogInformation("Seq API Key: {ApiKeyStatus}",
    string.IsNullOrEmpty(seqApiKey) ? "NOT SET" : $"SET (length: {seqApiKey.Length})");
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

app.Logger.LogInformation("MCP Tools: 3 (search, signals, sql)");
app.Logger.LogInformation("MCP Resources: 5 (events, errors, warnings, signals, exceptions)");
app.Logger.LogInformation("MCP Prompts: 8 (анализ ошибок, исключения, активность, безопасность и др.)");

await app.RunAsync();
