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
}
