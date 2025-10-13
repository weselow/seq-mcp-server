using SeqMcp.Models;

namespace SeqMcp.Services;

public interface ISeqApiClient : IDisposable
{
    Task<SearchEventsResult> SearchEventsAsync(string filter, int limit = 100);
    Task<ListSignalsResult> ListSignalsAsync();
    Task<ExecuteSqlResult> ExecuteSqlAsync(string query);
}
