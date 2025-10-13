namespace SeqMcp.Configuration;

/// <summary>
/// Per-request scoped context for Seq MCP server configuration.
/// Allows HTTP headers to override default configuration values.
/// </summary>
public class SeqRequestContext
{
    /// <summary>
    /// Project scope for filtering Seq events (e.g., "MyProject").
    /// Extracted from X-Seq-Project-Scope header or null if not provided.
    /// </summary>
    public string? ProjectScope { get; set; }

    /// <summary>
    /// Field name to use for scope filtering (e.g., "Application", "Environment").
    /// Extracted from X-Seq-Scope-Field header or null if not provided.
    /// </summary>
    public string? ScopeField { get; set; }
}
