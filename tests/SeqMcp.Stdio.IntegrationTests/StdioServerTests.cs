using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace SeqMcp.Stdio.IntegrationTests;

/// <summary>
/// Integration tests that drive <c>SeqMcp.Stdio.dll</c> as an out-of-process
/// MCP server via <see cref="Process"/> and exchange JSON-RPC frames over
/// stdin/stdout.
///
/// These tests intentionally do NOT require a running Seq instance for
/// the protocol-level checks (initialize, tools/list, stdout cleanness):
/// the server does not contact Seq during initialize or tool listing.
/// </summary>
public sealed class StdioServerTests : IDisposable
{
    private const int ProcessTimeoutMs = 30_000;

    private readonly Process _process;
    private readonly StringBuilder _stderrBuffer = new();
    private readonly StringBuilder _stdoutBuffer = new();

    public StdioServerTests()
    {
        var dllPath = LocateStdioDll();

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
            WorkingDirectory = Path.GetDirectoryName(dllPath)!,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Force a non-existent Seq URL so the server never tries to make
        // outbound calls during these tests. Initialize/tools-list don't
        // contact Seq, but it's defensive against future regressions.
        psi.Environment["SEQ_URL"] = "http://127.0.0.1:65535";
        psi.Environment["SEQ_API_KEY"] = "";

        _process = new Process { StartInfo = psi };
        _process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) _stderrBuffer.AppendLine(e.Data);
        };

        _process.Start();
        _process.BeginErrorReadLine();
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // process may already be gone
        }
        _process.Dispose();
    }

    [Fact]
    public async Task Initialize_Returns_ServerInfo()
    {
        var response = await InitializeAsync();

        response.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        response.GetProperty("id").GetInt32().Should().Be(1);
        response.TryGetProperty("result", out var result).Should().BeTrue(
            "initialize must produce a non-error result");
        result.TryGetProperty("serverInfo", out _).Should().BeTrue();
        result.TryGetProperty("protocolVersion", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ToolsList_After_Initialize_Returns_Seven_Tools()
    {
        await InitializeAsync();
        await SendNotificationAsync("notifications/initialized");

        var response = await SendRequestAsync(2, "tools/list");
        response.GetProperty("jsonrpc").GetString().Should().Be("2.0");
        response.TryGetProperty("result", out var result).Should().BeTrue();

        var tools = result.GetProperty("tools").EnumerateArray().ToList();
        tools.Should().HaveCount(7,
            "SeqTools exposes 7 MCP tools (search, list_signals, sql, " +
            "create_signal, update_signal, delete_signal, get_apps)");

        var names = tools.Select(t => t.GetProperty("name").GetString()).ToList();
        names.Should().Contain("seq_search_events");
        names.Should().Contain("seq_list_signals");
    }

    [Fact]
    public async Task Stdout_Contains_Only_Valid_JsonRpc_Frames()
    {
        await InitializeAsync();
        await SendNotificationAsync("notifications/initialized");
        await SendRequestAsync(2, "tools/list");

        // Every line received on stdout up to this point must be a valid
        // JSON-RPC frame. The presence of any non-JSON line (e.g. a log
        // message that leaked from ILogger) would break MCP clients.
        var stdoutLines = _stdoutBuffer.ToString()
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.Length > 0)
            .ToList();

        stdoutLines.Should().NotBeEmpty();
        foreach (var line in stdoutLines)
        {
            Action parse = () =>
            {
                using var doc = JsonDocument.Parse(line);
                doc.RootElement.TryGetProperty("jsonrpc", out var jsonrpc)
                    .Should().BeTrue($"line is not a JSON-RPC frame: {line}");
                jsonrpc.GetString().Should().Be("2.0");
            };
            parse.Should().NotThrow($"stdout must contain only JSON-RPC frames, got: {line}");
        }
    }

    [Fact(Skip = "Requires running Seq server")]
    public async Task ToolsCall_SearchEvents_Returns_Result()
    {
        await InitializeAsync();
        await SendNotificationAsync("notifications/initialized");

        var response = await SendRequestAsync(3, "tools/call", new
        {
            name = "seq_search_events",
            arguments = new { filter = "", limit = 10 },
        });

        response.TryGetProperty("result", out _).Should().BeTrue();
    }

    private async Task<JsonElement> InitializeAsync()
    {
        return await SendRequestAsync(1, "initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "stdio-integration-test", version = "0.1" },
        });
    }

    private async Task<JsonElement> SendRequestAsync(
        int id, string method, object? @params = null)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            id,
            method,
            @params = @params ?? new { },
        };
        var json = JsonSerializer.Serialize(payload);
        await WriteFrameAsync(json);
        var line = await ReadResponseLineAsync(id);
        return JsonDocument.Parse(line).RootElement.Clone();
    }

    private async Task SendNotificationAsync(string method, object? @params = null)
    {
        var payload = new
        {
            jsonrpc = "2.0",
            method,
            @params = @params ?? new { },
        };
        var json = JsonSerializer.Serialize(payload);
        await WriteFrameAsync(json);
    }

    private async Task WriteFrameAsync(string json)
    {
        await _process.StandardInput.WriteLineAsync(json);
        await _process.StandardInput.FlushAsync();
    }

    /// <summary>
    /// Reads lines from stdout until a JSON-RPC frame with the matching id
    /// arrives. Buffers every received line into <see cref="_stdoutBuffer"/>
    /// so the cleanness check can later inspect everything that was
    /// written to stdout.
    /// </summary>
    private async Task<string> ReadResponseLineAsync(int expectedId)
    {
        using var cts = new CancellationTokenSource(ProcessTimeoutMs);
        while (!cts.IsCancellationRequested)
        {
            var line = await _process.StandardOutput.ReadLineAsync(cts.Token);
            if (line is null) break;
            _stdoutBuffer.AppendLine(line);
            if (TryMatchId(line, expectedId)) return line;
        }
        throw new TimeoutException(
            $"Did not receive JSON-RPC response with id={expectedId} " +
            $"within {ProcessTimeoutMs}ms. Stderr:\n{_stderrBuffer}");
    }

    private static bool TryMatchId(string line, int expectedId)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("id", out var idProp)) return false;
            return idProp.ValueKind == JsonValueKind.Number
                && idProp.GetInt32() == expectedId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string LocateStdioDll()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "SeqMcp.Stdio.dll");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"SeqMcp.Stdio.dll not found at expected location: {path}. " +
                "ProjectReference should copy it to the test output directory.",
                path);
        }
        return path;
    }
}
