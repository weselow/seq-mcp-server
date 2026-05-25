using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeqMcp.Core.Configuration;

namespace SeqMcp.Core.Hosting;

/// <summary>
/// Loads <see cref="SeqOptions"/> reproducing the legacy per-field priority
/// from the original <c>Program.cs:17-31</c> loader.
///
/// Priority table (first non-empty wins):
/// <list type="table">
///   <item><c>Url</c>: env <c>SEQ_URL</c> → env <c>SEQ_SERVER_URL</c> → <c>Seq:Url</c> → default <c>http://localhost:8080</c></item>
///   <item><c>ApiKey</c>: env <c>SEQ_API_KEY</c> → <c>Seq:ApiKey</c></item>
///   <item><c>ProjectScope</c>: <c>Seq:ProjectScope</c> → env <c>SEQ_PROJECT_SCOPE</c></item>
///   <item><c>ScopeField</c>: <c>Seq:ScopeField</c> → env <c>SEQ_SCOPE_FIELD</c> → default <c>Application</c></item>
/// </list>
///
/// The asymmetry (env-wins for Url/ApiKey, appsettings-wins for
/// ProjectScope/ScopeField) is intentional — changing it would break
/// existing docker users.
/// </summary>
public static class SeqOptionsLoader
{
    public static SeqOptions Load(IConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var url = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SEQ_URL"),
            Environment.GetEnvironmentVariable("SEQ_SERVER_URL"),
            configuration["Seq:Url"]
        ) ?? "http://localhost:8080";

        var apiKey = FirstNonEmpty(
            Environment.GetEnvironmentVariable("SEQ_API_KEY"),
            configuration["Seq:ApiKey"]
        );

        var projectScope = FirstNonEmpty(
            configuration["Seq:ProjectScope"],
            Environment.GetEnvironmentVariable("SEQ_PROJECT_SCOPE")
        );

        var scopeField = FirstNonEmpty(
            configuration["Seq:ScopeField"],
            Environment.GetEnvironmentVariable("SEQ_SCOPE_FIELD")
        ) ?? "Application";

        return new SeqOptions
        {
            Url = url,
            ApiKey = apiKey,
            ProjectScope = projectScope,
            ScopeField = scopeField,
        };
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}

public static class SeqOptionsExtensions
{
    /// <summary>
    /// Registers <see cref="SeqOptions"/> with the legacy per-field priority
    /// (see <see cref="SeqOptionsLoader"/>). Use with
    /// <c>IOptions&lt;SeqOptions&gt;</c> in consumer constructors.
    /// </summary>
    public static IServiceCollection AddSeqOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var loaded = SeqOptionsLoader.Load(configuration);

        services.Configure<SeqOptions>(options =>
        {
            options.Url = loaded.Url;
            options.ApiKey = loaded.ApiKey;
            options.ProjectScope = loaded.ProjectScope;
            options.ScopeField = loaded.ScopeField;
        });

        return services;
    }
}
