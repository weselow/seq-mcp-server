namespace SeqMcp.Models;

public record SearchEventsResult(
    List<SeqEvent> Events,
    int TotalCount);
