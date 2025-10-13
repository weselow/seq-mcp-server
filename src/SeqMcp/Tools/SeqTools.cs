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

    [McpServerTool(Name = "seq_create_signal")]
    [Description("Создание нового сигнала/алерта Seq")]
    public async Task<string> CreateSignal(
        [Description("Название сигнала")]
        string title,
        [Description("Описание (опционально)")]
        string? description = null,
        [Description("Фильтр Seq (опционально)")]
        string? filter = null,
        [Description("Защищённый сигнал (по умолчанию false)")]
        bool isProtected = false)
    {
        var result = await _seqClient.CreateSignalAsync(title, description, filter, isProtected);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "seq_update_signal")]
    [Description("Обновление существующего сигнала")]
    public async Task<string> UpdateSignal(
        [Description("ID сигнала")]
        string signalId,
        [Description("Новое название (опционально)")]
        string? title = null,
        [Description("Новое описание (опционально)")]
        string? description = null,
        [Description("Новый фильтр (опционально)")]
        string? filter = null)
    {
        var result = await _seqClient.UpdateSignalAsync(signalId, title, description, filter);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "seq_delete_signal")]
    [Description("Удаление сигнала по ID")]
    public async Task<string> DeleteSignal(
        [Description("ID сигнала для удаления")]
        string signalId)
    {
        var result = await _seqClient.DeleteSignalAsync(signalId);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerTool(Name = "seq_get_apps")]
    [Description("Список приложений логирующих в Seq")]
    public async Task<string> GetApplications(
        [Description("Лимит приложений (по умолчанию 50)")]
        int limit = 50)
    {
        var result = await _seqClient.GetApplicationsAsync(limit);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }
}
