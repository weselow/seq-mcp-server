using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Hosting;

namespace SeqMcp.Tests.Configuration;

/// <summary>
/// Tests for SeqOptionsLoader — verifies per-field priority between
/// env-vars and appsettings is reproduced 1:1 from the legacy Program.cs:17-31.
///
/// Priority table (first non-empty wins):
///   Url:          env SEQ_URL → env SEQ_SERVER_URL → Seq:Url → default
///   ApiKey:       env SEQ_API_KEY → Seq:ApiKey
///   ProjectScope: Seq:ProjectScope → env SEQ_PROJECT_SCOPE      (appsettings wins!)
///   ScopeField:   Seq:ScopeField → env SEQ_SCOPE_FIELD → default (appsettings wins!)
///
/// Each test uses EnvScope to set/clear env-vars deterministically.
/// </summary>
public class SeqOptionsLoaderTests
{
    private static readonly string[] AllEnvVars =
    {
        "SEQ_URL",
        "SEQ_SERVER_URL",
        "SEQ_API_KEY",
        "SEQ_PROJECT_SCOPE",
        "SEQ_SCOPE_FIELD",
        "SEQ_BLOCK_PRIVATE_HOSTS",
        "SEQ_ALLOW_URL_OVERRIDE",
        "Seq__Url",
    };

    /// <summary>
    /// Sets env vars on construction, restores prior values on Dispose.
    /// </summary>
    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new();

        public EnvScope(Dictionary<string, string?> vars)
        {
            // Snapshot ALL relevant env-vars (not just the ones being set),
            // so that a polluted CI environment can't leak into the test.
            foreach (var name in AllEnvVars)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, null);
            }

            foreach (var (key, value) in vars)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public void Dispose()
        {
            foreach (var (key, value) in _previous)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?>? appsettings = null)
    {
        var builder = new ConfigurationBuilder();
        if (appsettings is { Count: > 0 })
        {
            builder.AddInMemoryCollection(appsettings);
        }
        return builder.Build();
    }

    [Fact]
    public void Should_Use_Defaults_When_Nothing_Configured()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.Url.Should().Be("http://localhost:8080");
        options.ApiKey.Should().BeNull();
        options.ProjectScope.Should().BeNull();
        options.ScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Read_Url_From_SEQ_URL_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_URL"] = "http://from-env:9999"
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:Url"] = "http://from-appsettings:1111"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — env wins over appsettings for Url
        options.Url.Should().Be("http://from-env:9999");
    }

    [Fact]
    public void Should_Read_Url_From_SEQ_SERVER_URL_Alias_When_SEQ_URL_Is_Missing()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_SERVER_URL"] = "http://alias-env:8888"
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.Url.Should().Be("http://alias-env:8888");
    }

    [Fact]
    public void Should_Prefer_SEQ_URL_Over_SEQ_SERVER_URL_When_Both_Set()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_URL"] = "http://primary:1",
            ["SEQ_SERVER_URL"] = "http://alias:2"
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — SEQ_URL wins
        options.Url.Should().Be("http://primary:1");
    }

    [Fact]
    public void Should_Read_Url_From_AppSettings_When_No_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:Url"] = "http://from-appsettings:5341"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.Url.Should().Be("http://from-appsettings:5341");
    }

    [Fact]
    public void Should_Treat_Seq__Url_Env_Like_AppSettings_Priority()
    {
        // Arrange
        // Structural env var Seq__Url binds into Seq:Url in IConfiguration.
        // SEQ_URL should win over it (env-priority for Url is SEQ_URL → SEQ_SERVER_URL → Seq:Url).
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_URL"] = "http://legacy-env:1",
            ["Seq__Url"] = "http://structural-env:2"
        });
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — legacy SEQ_URL still wins over Seq__Url (which lives at appsettings priority)
        options.Url.Should().Be("http://legacy-env:1");
    }

    [Fact]
    public void Should_Read_ApiKey_From_Env_Over_AppSettings()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_API_KEY"] = "env-api-key"
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:ApiKey"] = "appsettings-api-key"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — env wins over appsettings for ApiKey
        options.ApiKey.Should().Be("env-api-key");
    }

    [Fact]
    public void Should_Read_ApiKey_From_AppSettings_When_No_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:ApiKey"] = "appsettings-api-key"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.ApiKey.Should().Be("appsettings-api-key");
    }

    [Fact]
    public void Should_Prefer_AppSettings_ProjectScope_Over_Env()
    {
        // Arrange — CRITICAL: reverse priority — appsettings wins for ProjectScope
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_PROJECT_SCOPE"] = "env-project"
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:ProjectScope"] = "appsettings-project"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — appsettings wins
        options.ProjectScope.Should().Be("appsettings-project");
    }

    [Fact]
    public void Should_Read_ProjectScope_From_Env_When_No_AppSettings()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_PROJECT_SCOPE"] = "env-only-project"
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.ProjectScope.Should().Be("env-only-project");
    }

    [Fact]
    public void Should_Prefer_AppSettings_ScopeField_Over_Env()
    {
        // Arrange — CRITICAL: reverse priority — appsettings wins for ScopeField
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_SCOPE_FIELD"] = "EnvField"
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:ScopeField"] = "AppsettingsField"
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert — appsettings wins
        options.ScopeField.Should().Be("AppsettingsField");
    }

    [Fact]
    public void Should_Read_ScopeField_From_Env_When_No_AppSettings()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_SCOPE_FIELD"] = "Environment"
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.ScopeField.Should().Be("Environment");
    }

    [Fact]
    public void Should_Default_ScopeField_To_Application_When_Both_Empty()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.ScopeField.Should().Be("Application");
    }

    [Fact]
    public void Should_Default_BlockPrivateHosts_To_False()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.BlockPrivateHosts.Should().BeFalse();
    }

    [Fact]
    public void Should_Read_BlockPrivateHosts_From_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_BLOCK_PRIVATE_HOSTS"] = "true",
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.BlockPrivateHosts.Should().BeTrue();
    }

    [Fact]
    public void Should_Read_BlockPrivateHosts_From_AppSettings_When_No_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:BlockPrivateHosts"] = "true",
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.BlockPrivateHosts.Should().BeTrue();
    }

    [Fact]
    public void Should_Prefer_Env_BlockPrivateHosts_Over_AppSettings()
    {
        // Arrange — env-first priority matches Url/ApiKey
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_BLOCK_PRIVATE_HOSTS"] = "false",
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:BlockPrivateHosts"] = "true",
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.BlockPrivateHosts.Should().BeFalse();
    }

    [Fact]
    public void Should_Treat_Invalid_BlockPrivateHosts_As_False()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_BLOCK_PRIVATE_HOSTS"] = "not-a-bool",
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.BlockPrivateHosts.Should().BeFalse();
    }

    [Fact]
    public void Should_Default_AllowUrlOverride_To_False()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.AllowUrlOverride.Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("YES")]
    public void Should_Read_AllowUrlOverride_From_Env_Truthy(string raw)
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_ALLOW_URL_OVERRIDE"] = raw,
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.AllowUrlOverride.Should().BeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("no")]
    [InlineData("anything-else")]
    [InlineData("  ")]
    public void Should_Read_AllowUrlOverride_From_Env_Falsy(string raw)
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_ALLOW_URL_OVERRIDE"] = raw,
        });
        var config = BuildConfig();

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.AllowUrlOverride.Should().BeFalse();
    }

    [Fact]
    public void Should_Read_AllowUrlOverride_From_AppSettings_When_No_Env()
    {
        // Arrange
        using var env = new EnvScope(new Dictionary<string, string?>());
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:AllowUrlOverride"] = "true",
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.AllowUrlOverride.Should().BeTrue();
    }

    [Fact]
    public void Should_Prefer_Env_AllowUrlOverride_Over_AppSettings()
    {
        // Arrange — env-first priority matches BlockPrivateHosts
        using var env = new EnvScope(new Dictionary<string, string?>
        {
            ["SEQ_ALLOW_URL_OVERRIDE"] = "false",
        });
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["Seq:AllowUrlOverride"] = "true",
        });

        // Act
        var options = SeqOptionsLoader.Load(config);

        // Assert
        options.AllowUrlOverride.Should().BeFalse();
    }
}
