namespace SeqMcp.Core.Configuration;

public class SeqServerConfig
{
    public string ServerUrl { get; }
    public string? ApiKey { get; }
    public int DefaultEventLimit { get; }

    /// <summary>
    /// Default project scope for filtering events (optional).
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string? DefaultProjectScope { get; }

    /// <summary>
    /// Field name to use for scope filtering (default: "Application").
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string DefaultScopeField { get; }

    public SeqServerConfig(
        string serverUrl,
        string? apiKey = null,
        string? defaultProjectScope = null,
        string? defaultScopeField = null)
    {
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new ArgumentException(
                "ServerUrl cannot be null or whitespace",
                nameof(serverUrl));
        }

        ServerUrl = serverUrl;
        ApiKey = apiKey;
        DefaultEventLimit = 100;
        DefaultProjectScope = defaultProjectScope;
        DefaultScopeField = defaultScopeField ?? "Application"; // Default field name
    }
}
