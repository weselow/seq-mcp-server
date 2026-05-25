namespace SeqMcp.Core.Models;

public record ListSignalsResult(
    List<SeqSignal> Signals,
    int TotalCount);
