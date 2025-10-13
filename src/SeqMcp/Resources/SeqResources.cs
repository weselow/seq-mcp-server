using System.ComponentModel;
using ModelContextProtocol.Server;
using SeqMcp.Services;

namespace SeqMcp.Resources;

[McpServerResourceType]
public class SeqResources
{
    private readonly ISeqApiClient _seqClient;

    public SeqResources(ISeqApiClient seqClient)
    {
        _seqClient = seqClient;
    }

    [McpServerResource(UriTemplate = "seq://events/latest")]
    [Description("Последние 50 событий из Seq")]
    public async Task<string> GetLatestEvents()
    {
        var result = await _seqClient.SearchEventsAsync("", 50);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://events/errors")]
    [Description("Последние 50 ошибок (Error + Fatal)")]
    public async Task<string> GetErrorEvents()
    {
        var result = await _seqClient.SearchEventsAsync("Level in ['Error', 'Fatal']", 50);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://events/warnings")]
    [Description("Последние 50 предупреждений")]
    public async Task<string> GetWarningEvents()
    {
        var result = await _seqClient.SearchEventsAsync("Level = 'Warning'", 50);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://signals")]
    [Description("Все сохраненные сигналы Seq")]
    public async Task<string> GetSignals()
    {
        var result = await _seqClient.ListSignalsAsync();
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://events/exceptions")]
    [Description("События с исключениями (последние 50)")]
    public async Task<string> GetExceptionEvents()
    {
        var result = await _seqClient.SearchEventsAsync("@Exception is not null", 50);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://events/last-hour")]
    [Description("События за последний час (все уровни)")]
    public async Task<string> GetLastHourEvents()
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss");
        var filter = $"@Timestamp >= DateTime('{oneHourAgo}')";
        var result = await _seqClient.SearchEventsAsync(filter, 100);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://events/today")]
    [Description("События за сегодня (все уровни)")]
    public async Task<string> GetTodayEvents()
    {
        var todayStart = DateTime.UtcNow.Date.ToString("yyyy-MM-ddTHH:mm:ss");
        var filter = $"@Timestamp >= DateTime('{todayStart}')";
        var result = await _seqClient.SearchEventsAsync(filter, 200);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://performance/slow")]
    [Description("Медленные операции (Elapsed > 1000ms, последние 50)")]
    public async Task<string> GetSlowPerformanceEvents()
    {
        var filter = "@Properties.Elapsed > 1000 or @Properties.ElapsedMilliseconds > 1000 or @Properties.Duration > 1000";
        var result = await _seqClient.SearchEventsAsync(filter, 50);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }

    [McpServerResource(UriTemplate = "seq://stats/summary")]
    [Description("Статистика событий за последний час по уровням")]
    public async Task<string> GetEventsSummary()
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ss");
        var sqlQuery = $@"
            select Level, count(*) as Count
            from stream
            where @Timestamp >= DateTime('{oneHourAgo}')
            group by Level
            order by Count desc";

        var result = await _seqClient.ExecuteSqlAsync(sqlQuery);
        return System.Text.Json.JsonSerializer.Serialize(result);
    }
}
