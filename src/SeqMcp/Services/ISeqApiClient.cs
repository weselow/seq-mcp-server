using SeqMcp.Models;

namespace SeqMcp.Services;

public interface ISeqApiClient : IDisposable
{
    Task<SearchEventsResult> SearchEventsAsync(string filter, int limit = 100);
    Task<ListSignalsResult> ListSignalsAsync();
    Task<ExecuteSqlResult> ExecuteSqlAsync(string query);
    Task<CreateSignalResult> CreateSignalAsync(string title, string? description, string? filter, bool isProtected = false);
    Task<UpdateSignalResult> UpdateSignalAsync(string signalId, string? title = null, string? description = null, string? filter = null);
    Task<DeleteSignalResult> DeleteSignalAsync(string signalId);
    Task<GetApplicationsResult> GetApplicationsAsync(int limit = 50);
}
