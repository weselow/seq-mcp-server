namespace SeqMcp.Configuration;

public class SeqServerConfig
{
    public string ServerUrl { get; }
    public string? ApiKey { get; }
    public int DefaultEventLimit { get; }

    public SeqServerConfig(string serverUrl, string? apiKey = null)
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
    }
}
