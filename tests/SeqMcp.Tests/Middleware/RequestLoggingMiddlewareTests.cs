using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SeqMcp.Middleware;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Middleware;

public class RequestLoggingMiddlewareTests
{
    private static DefaultHttpContext CreateContext(
        string method = "POST",
        string path = "/mcp",
        string body = "",
        Stream? responseBody = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        ctx.Response.Body = responseBody ?? new MemoryStream();
        return ctx;
    }

    [Fact]
    public async Task Should_Not_Replace_Response_Body_Stream()
    {
        // Arrange
        var originalBody = new MemoryStream();
        var ctx = CreateContext(responseBody: originalBody);
        Stream? bodyDuringNext = null;

        RequestDelegate next = c =>
        {
            bodyDuringNext = c.Response.Body;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, new RecordingLogger<RequestLoggingMiddleware>());

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — middleware НЕ должен подменять Response.Body
        // (это ломает SSE-streaming MCP — события клиенту приходят только после Close)
        bodyDuringNext.Should().BeSameAs(originalBody);
    }

    [Fact]
    public async Task Should_Not_Read_Request_Body_Into_Memory()
    {
        // Arrange — поток request body, который сразу регистрирует любую попытку чтения
        var trackedBody = new ReadTrackingStream(new MemoryStream(Encoding.UTF8.GetBytes("{\"big\":\"payload\"}")));
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/mcp";
        ctx.Request.Body = trackedBody;
        ctx.Response.Body = new MemoryStream();

        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, new RecordingLogger<RequestLoggingMiddleware>());

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — middleware не читает body для логирования.
        // Если включён Debug и оператор хочет логировать тела — это отдельная фича,
        // но дефолтное поведение (без Debug-логгера) не должно читать body вообще.
        trackedBody.WasRead.Should().BeFalse("middleware must not read request body by default");
    }

    [Fact]
    public async Task Should_Log_Metadata_At_Debug_Level_Only()
    {
        // Arrange
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var ctx = CreateContext(method: "POST", path: "/mcp");

        var middleware = new RequestLoggingMiddleware(
            c => { c.Response.StatusCode = 200; return Task.CompletedTask; },
            logger);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — никаких Information записей (только Debug или ниже)
        logger.Records.Should().NotContain(r => r.Level >= LogLevel.Information,
            "request/response logging must be Debug-only — by default not visible in prod");
    }

    [Fact]
    public async Task Should_Log_Metadata_When_Debug_Enabled()
    {
        // Arrange
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var ctx = CreateContext(method: "POST", path: "/mcp");

        var middleware = new RequestLoggingMiddleware(
            c => { c.Response.StatusCode = 201; return Task.CompletedTask; },
            logger);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — есть Debug-запись с метаданными
        logger.Records.Should().Contain(r =>
            r.Level == LogLevel.Debug &&
            r.Message.Contains("POST") &&
            r.Message.Contains("/mcp") &&
            r.Message.Contains("201"));
    }

    [Fact]
    public async Task Should_Not_Log_Request_Body_Content_Even_At_Debug()
    {
        // Дефолтная политика — body НЕ логируется вообще.
        // Если будущий бид введёт `SEQ_LOG_REQUEST_BODY=true` — это будет отдельный код-пас.
        // Arrange
        var sensitivePayload = "{\"ApiKey\":\"super-secret\"}";
        var logger = new RecordingLogger<RequestLoggingMiddleware>();
        var ctx = CreateContext(body: sensitivePayload);

        var middleware = new RequestLoggingMiddleware(_ => Task.CompletedTask, logger);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        logger.Records.Should().NotContain(r => r.Message.Contains("super-secret"),
            "raw request bodies must never reach logs");
        logger.Records.Should().NotContain(r => r.Message.Contains("ApiKey"),
            "even masked, sensitive keys should not surface in metadata logs");
    }

    [Fact]
    public async Task Should_Call_Next_Once()
    {
        // Arrange
        var callCount = 0;
        var ctx = CreateContext();
        var middleware = new RequestLoggingMiddleware(
            _ => { callCount++; return Task.CompletedTask; },
            new RecordingLogger<RequestLoggingMiddleware>());

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task Should_Propagate_Exception_From_Next()
    {
        // Arrange — middleware не должен глушить ошибки
        var ctx = CreateContext();
        var middleware = new RequestLoggingMiddleware(
            _ => throw new InvalidOperationException("downstream"),
            new RecordingLogger<RequestLoggingMiddleware>());

        // Act
        var act = async () => await middleware.InvokeAsync(ctx);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("downstream");
    }

    private sealed class ReadTrackingStream : Stream
    {
        private readonly Stream _inner;
        public bool WasRead { get; private set; }

        public ReadTrackingStream(Stream inner) => _inner = inner;

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            WasRead = true;
            return _inner.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            WasRead = true;
            return _inner.ReadAsync(buffer, offset, count, ct);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            WasRead = true;
            return _inner.ReadAsync(buffer, ct);
        }

        public override void Flush() => _inner.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    }
}
