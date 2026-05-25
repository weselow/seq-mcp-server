namespace SeqMcp.Core.Configuration;

/// <summary>
/// Strongly-typed Seq configuration, bound via <c>SeqOptionsLoader.Load</c>
/// (or <c>SeqOptionsExtensions.AddSeqOptions</c> for DI).
///
/// Field-level priority is intentionally asymmetric to preserve the legacy
/// behavior of the original <c>Program.cs:17-31</c> loader. See
/// <see cref="SeqMcp.Core.Hosting.SeqOptionsLoader"/> for the priority table.
/// </summary>
public class SeqOptions
{
    public string Url { get; set; } = "http://localhost:8080";

    public string? ApiKey { get; set; }

    /// <summary>
    /// Default project scope for filtering events (optional).
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string? ProjectScope { get; set; }

    /// <summary>
    /// Field name to use for scope filtering (default: "Application").
    /// Used as fallback when no HTTP header is provided.
    /// </summary>
    public string ScopeField { get; set; } = "Application";
}
