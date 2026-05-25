namespace SeqMcp.Core.Models;

public record SearchEventsResult(
    List<SeqEvent> Events,
    int TotalCount);
