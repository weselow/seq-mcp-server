using SeqMcp.Core.Models;

namespace SeqMcp.Core.Services;

/// <summary>
/// Seq API client. Lifetime: Scoped (HTTP) or Singleton (stdio).
/// No longer <see cref="IDisposable"/> — all connection resources are
/// owned by <see cref="ISeqConnectionFactory"/>; each method acquires a
/// short-lived <see cref="IConnectionLease"/> per call.
/// </summary>
public interface ISeqApiClient
{
    Task<SearchEventsResult> SearchEventsAsync(string filter, int limit = 100);
    Task<ListSignalsResult> ListSignalsAsync();
    Task<ExecuteSqlResult> ExecuteSqlAsync(string query);
    Task<CreateSignalResult> CreateSignalAsync(string title, string? description, string? filter, bool isProtected = false);
    Task<UpdateSignalResult> UpdateSignalAsync(string signalId, string? title = null, string? description = null, string? filter = null);
    Task<DeleteSignalResult> DeleteSignalAsync(string signalId);
    Task<GetApplicationsResult> GetApplicationsAsync(int limit = 50);
}
