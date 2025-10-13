namespace SeqMcp.Models;

public record SeqEvent(
    string Id,
    string Timestamp,
    string Level,
    string? RenderedMessage,
    string? Exception);
