using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Hosting;
using SeqMcp.Core.Prompts;
using SeqMcp.Core.Resources;
using SeqMcp.Core.Services;
using SeqMcp.Core.Tools;

var builder = Host.CreateApplicationBuilder(args);

// CRITICAL — for stdio transport stdout is the JSON-RPC channel; any byte
// written to it that is not a JSON-RPC frame breaks the protocol.
//
// 1. ClearProviders MUST be first. Host.CreateApplicationBuilder adds the
//    default console provider which writes to stdout. AddConsole on top of
//    it would not remove it — we would end up with two providers, one of
//    them still on stdout, and the MCP client would see corrupted frames.
builder.Logging.ClearProviders();

// 2. The only console provider, redirected entirely to stderr.
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Seq options — reused compat loader from Core (env > appsettings priority
// for Url/ApiKey, see SeqOptionsLoader for the full table).
builder.Services.AddSeqOptions(builder.Configuration);

// DI scope — Singleton everywhere. One process serves exactly one Seq
// instance; there are no HTTP requests, no per-call scope headers. Keeping
// SeqRequestContext as a Singleton with empty fields keeps the
// SeqApiClient constructor signature uniform across both entry points.
builder.Services.AddSingleton<ISeqConnectionFactory, SeqConnectionFactory>();
builder.Services.AddSingleton<SeqRequestContext>();
builder.Services.AddSingleton<ISeqApiClient, SeqApiClient>();
builder.Services.AddSingleton<SeqTools>();
builder.Services.AddSingleton<SeqResources>();
builder.Services.AddSingleton<SeqPrompts>();
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();

// MCP server with stdio transport + shared primitives from Core.
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .AddSeqMcpPrimitives();

var host = builder.Build();

// Log startup details to stderr so the user can verify configuration
// without polluting the JSON-RPC stdout channel.
var startupLogger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("SeqMcp.Stdio");
var startupOptions = host.Services.GetRequiredService<
    Microsoft.Extensions.Options.IOptions<SeqOptions>>().Value;
startupLogger.LogInformation("Seq MCP Stdio server starting...");
startupLogger.LogInformation("Seq URL: {SeqUrl}", startupOptions.Url);
startupLogger.LogInformation("Seq API Key: {ApiKeyStatus}",
    string.IsNullOrEmpty(startupOptions.ApiKey)
        ? "NOT SET"
        : $"SET (length: {startupOptions.ApiKey.Length})");
startupLogger.LogInformation("Transport: stdio (JSON-RPC over stdin/stdout)");

await host.RunAsync();
