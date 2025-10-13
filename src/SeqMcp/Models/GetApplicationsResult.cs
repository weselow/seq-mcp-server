namespace SeqMcp.Models;

public record GetApplicationsResult(
    List<SeqApplication> Applications,
    int TotalCount);
