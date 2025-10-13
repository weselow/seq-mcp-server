namespace SeqMcp.Models;

public record ListSignalsResult(
    List<SeqSignal> Signals,
    int TotalCount);
