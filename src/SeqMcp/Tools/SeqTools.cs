using System.ComponentModel;
using ModelContextProtocol.Server;
using SeqMcp.Services;

namespace SeqMcp.Tools;

[McpServerToolType]
public class SeqTools
{
    private readonly ISeqApiClient _seqClient;

    public SeqTools(ISeqApiClient seqClient)
    {
        _seqClient = seqClient;
    }

    [McpServerTool(Name = "seq_search_events")]
    [Description("Поиск событий Seq с фильтрацией")]
    public async Task<string> SearchEvents(
        [Description("Фильтр Seq (Level='Error', @Exception!=null) или пусто")]
        string filter = "",
        [Description("Лимит событий (по умолчанию 100)")]
        int limit = 100)
    {
        var result = await _seqClient.SearchEventsAsync(filter, limit);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "seq_list_signals")]
    [Description("Список сохранённых сигналов Seq")]
    public async Task<string> ListSignals()
    {
        var result = await _seqClient.ListSignalsAsync();
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "seq_execute_sql")]
    [Description("SQL запрос к логам Seq")]
    public async Task<string> ExecuteSql(
        [Description("SQL запрос (select count(*) from stream where Level='Error')")]
        string query)
    {
        var result = await _seqClient.ExecuteSqlAsync(query);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }
}
