namespace SeqMcp.Core.Models;

public record GetApplicationsResult(
    List<SeqApplication> Applications,
    int TotalCount);
