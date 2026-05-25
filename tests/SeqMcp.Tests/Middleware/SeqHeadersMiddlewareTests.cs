using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Http.Middleware;
using SeqMcp.Tests.Helpers;

namespace SeqMcp.Tests.Middleware;

/// <summary>
/// Tests for <see cref="SeqHeadersMiddleware"/>: header extraction,
/// X-Seq-Url validation, and the AllowUrlOverride feature flag.
/// Uses a real <see cref="DefaultHttpContext"/> with a minimal service
/// provider containing <see cref="SeqRequestContext"/>.
/// </summary>
public class SeqHeadersMiddlewareTests
{
    private static (DefaultHttpContext Context, SeqRequestContext RequestContext, MemoryStream ResponseBody)
        CreateContext(IDictionary<string, string>? headers = null)
    {
        var services = new ServiceCollection();
        var requestContext = new SeqRequestContext();
        services.AddSingleton(requestContext);
        var provider = services.BuildServiceProvider();

        var ctx = new DefaultHttpContext { RequestServices = provider };
        ctx.Request.Method = "POST";
        ctx.Request.Path = "/mcp";
        var responseBody = new MemoryStream();
        ctx.Response.Body = responseBody;

        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                ctx.Request.Headers[k] = v;
            }
        }

        return (ctx, requestContext, responseBody);
    }

    private static SeqHeadersMiddleware Build(
        bool allowUrlOverride,
        RequestDelegate? next = null,
        ILogger<SeqHeadersMiddleware>? logger = null)
    {
        var options = Options.Create(new SeqOptions { AllowUrlOverride = allowUrlOverride });
        return new SeqHeadersMiddleware(
            next ?? (_ => Task.CompletedTask),
            logger ?? new RecordingLogger<SeqHeadersMiddleware>(),
            options);
    }

    [Fact]
    public async Task Should_Always_Read_X_Seq_ApiKey_Regardless_Of_Flag()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-ApiKey"] = "my-secret-key",
        });
        var middleware = Build(allowUrlOverride: false);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        requestContext.ApiKey.Should().Be("my-secret-key");
        requestContext.SeqUrl.Should().BeNull();
    }

    [Fact]
    public async Task Should_Read_Project_Scope_And_Field_Headers()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Project-Scope"] = "MyProject",
            ["X-Seq-Scope-Field"] = "Environment",
        });
        var middleware = Build(allowUrlOverride: false);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        requestContext.ProjectScope.Should().Be("MyProject");
        requestContext.ScopeField.Should().Be("Environment");
    }

    [Fact]
    public async Task Should_Ignore_X_Seq_Url_When_Override_Disabled()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://other.seq.local:8080",
        });
        var logger = new RecordingLogger<SeqHeadersMiddleware>();
        var middleware = Build(allowUrlOverride: false, logger: logger);
        var nextCalled = false;
        var middlewareWithNext = new SeqHeadersMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            logger,
            Options.Create(new SeqOptions { AllowUrlOverride = false }));

        // Act
        await middlewareWithNext.InvokeAsync(ctx);

        // Assert — context untouched, pipeline continues, warning logged
        requestContext.SeqUrl.Should().BeNull();
        nextCalled.Should().BeTrue("ignored X-Seq-Url is not a request error — pipeline must proceed");
        logger.Records.Should().Contain(r =>
            r.Level == LogLevel.Warning && r.Message.Contains("SEQ_ALLOW_URL_OVERRIDE"));
    }

    [Fact]
    public async Task Should_Not_Leak_ApiKey_In_Ignored_Url_Warning()
    {
        // Arrange
        var (ctx, _, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://other.seq.local:8080",
            ["X-Seq-ApiKey"] = "super-secret-key-do-not-leak",
        });
        var logger = new RecordingLogger<SeqHeadersMiddleware>();
        var middleware = Build(allowUrlOverride: false, logger: logger);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        logger.Records.Should().NotContain(r => r.Message.Contains("super-secret-key-do-not-leak"));
    }

    [Fact]
    public async Task Should_Emit_Ignored_Url_Warning_Only_Once_Per_Process()
    {
        // Arrange
        var logger = new RecordingLogger<SeqHeadersMiddleware>();
        var middleware = Build(allowUrlOverride: false, logger: logger);

        // Act
        for (var i = 0; i < 5; i++)
        {
            var (ctx, _, _) = CreateContext(new Dictionary<string, string>
            {
                ["X-Seq-Url"] = "http://other.seq.local:8080",
            });
            await middleware.InvokeAsync(ctx);
        }

        // Assert — single warning across many requests (anti-flood latch)
        logger.Records.Count(r =>
            r.Level == LogLevel.Warning && r.Message.Contains("SEQ_ALLOW_URL_OVERRIDE"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Should_Populate_SeqUrl_When_Override_Enabled_And_Url_Valid()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://tenant-b.seq.example.com:8080",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — Uri normalization adds a trailing slash for the path root
        requestContext.SeqUrl.Should().Be("http://tenant-b.seq.example.com:8080/");
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Should_Reject_File_Scheme_With_400()
    {
        // Arrange
        var (ctx, requestContext, body) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "file:///etc/passwd",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
        var responseText = Encoding.UTF8.GetString(body.ToArray());
        responseText.Should().Contain("scheme");
        responseText.Should().NotContain("/etc/passwd",
            "error body must not echo offending header value");
    }

    [Fact]
    public async Task Should_Reject_Url_With_Credentials_With_400()
    {
        // Arrange
        var (ctx, requestContext, body) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://attacker:pwn@host.example.com:8080",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
        var responseText = Encoding.UTF8.GetString(body.ToArray());
        responseText.Should().NotContain("attacker", "credentials must not be echoed back");
        responseText.Should().NotContain("pwn");
    }

    [Fact]
    public async Task Should_Reject_Url_With_Fragment_With_400()
    {
        // Arrange
        var (ctx, requestContext, body) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://host.example.com:8080/#frag",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
    }

    [Fact]
    public async Task Should_Reject_Malformed_Url_With_400()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "not a url at all",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
    }

    [Fact]
    public async Task Should_Reject_Url_With_Null_Byte_With_400()
    {
        // Arrange — null byte in host
        var (ctx, requestContext, body) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://host\0attack:8080",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
        var responseText = Encoding.UTF8.GetString(body.ToArray());
        responseText.Should().Contain("control");
    }

    [Fact]
    public async Task Should_Reject_Url_With_CrLf_Injection_With_400()
    {
        // Arrange — classic header-injection / log-injection payload
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "http://host.example.com:8080\r\nX-Evil: bad",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
    }

    [Fact]
    public async Task Should_Reject_Empty_Url_Override_With_400()
    {
        // Arrange — empty override is ambiguous; treat as bad input
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "   ",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        requestContext.SeqUrl.Should().BeNull();
    }

    [Fact]
    public async Task Should_Allow_Https_Scheme_When_Override_Enabled()
    {
        // Arrange
        var (ctx, requestContext, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "https://secure.seq.example.com",
        });
        var middleware = Build(allowUrlOverride: true);

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        requestContext.SeqUrl.Should().Be("https://secure.seq.example.com/");
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Should_Call_Next_When_No_Headers_Present()
    {
        // Arrange
        var (ctx, _, _) = CreateContext();
        var nextCalled = false;
        var middleware = new SeqHeadersMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new RecordingLogger<SeqHeadersMiddleware>(),
            Options.Create(new SeqOptions()));

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert
        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Should_Not_Call_Next_When_Url_Validation_Fails()
    {
        // Arrange
        var (ctx, _, _) = CreateContext(new Dictionary<string, string>
        {
            ["X-Seq-Url"] = "file:///etc/passwd",
        });
        var nextCalled = false;
        var middleware = new SeqHeadersMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            new RecordingLogger<SeqHeadersMiddleware>(),
            Options.Create(new SeqOptions { AllowUrlOverride = true }));

        // Act
        await middleware.InvokeAsync(ctx);

        // Assert — 400 short-circuits the pipeline
        nextCalled.Should().BeFalse();
        ctx.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }
}
