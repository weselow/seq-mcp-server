using Seq.Api;
using SeqMcp.Configuration;
using SeqMcp.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net;

namespace SeqMcp.Services;

public class SeqApiClient : ISeqApiClient
{
    private readonly SeqConnection _connection;
    private readonly HttpClient _httpClient;
    private readonly SeqServerConfig _config;
    private readonly ILogger<SeqApiClient> _logger;

    public SeqApiClient(
        HttpClient httpClient,
        SeqServerConfig config,
        ILogger<SeqApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogInformation(
            "Initializing SeqApiClient for server: {ServerUrl}",
            _config.ServerUrl);

        _connection = new SeqConnection(
            _config.ServerUrl,
            _config.ApiKey);
    }

    public async Task<SearchEventsResult> SearchEventsAsync(
        string filter,
        int limit = 100)
    {
        if (filter == null)
        {
            _logger.LogError("SearchEventsAsync called with null filter");
            throw new ArgumentException(
                "Filter cannot be null",
                nameof(filter));
        }

        if (limit < 0)
        {
            _logger.LogError(
                "SearchEventsAsync called with negative limit: {Limit}",
                limit);
            throw new ArgumentException(
                "Limit must be non-negative",
                nameof(limit));
        }

        try
        {
            _logger.LogInformation(
                "Searching events with filter: '{Filter}', limit: {Limit}",
                filter,
                limit);

            // Use direct HTTP request to /api/events to avoid WebSocket requirement
            var queryParams = new List<string>
            {
                $"count={limit}",
                "render=true"
            };

            if (!string.IsNullOrEmpty(filter))
            {
                queryParams.Add($"filter={WebUtility.UrlEncode(filter)}");
            }

            var url = $"/api/events?{string.Join("&", queryParams)}";
            _logger.LogInformation("Requesting: {Url}", url);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Seq API response: {Content}", content);

            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var events = new List<SeqEvent>();

            // Seq /api/events returns array directly (not wrapped in object)
            if (root.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Expected array from Seq API, got {Type}", root.ValueKind);
                return new SearchEventsResult(events, 0);
            }

            foreach (var evt in root.EnumerateArray())
            {
                // Seq API uses PascalCase field names (Timestamp, Level, RenderedMessage, Id)
                var seqEvent = new SeqEvent(
                    Id: evt.TryGetProperty("Id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                    Timestamp: evt.TryGetProperty("Timestamp", out var ts) ? ts.GetString() ?? string.Empty : string.Empty,
                    Level: evt.TryGetProperty("Level", out var lvl) ? lvl.GetString() ?? "Information" : "Information",
                    RenderedMessage: evt.TryGetProperty("RenderedMessage", out var msg) ? msg.GetString() : null,
                    Exception: evt.TryGetProperty("Exception", out var ex) ? ex.GetString() : null
                );
                events.Add(seqEvent);
            }

            _logger.LogInformation(
                "Found {EventCount} events",
                events.Count);

            return new SearchEventsResult(events, events.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "HTTP error while searching events: {Message}",
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while searching events");
            throw;
        }
    }

    public async Task<ListSignalsResult> ListSignalsAsync()
    {
        try
        {
            _logger.LogInformation("Listing signals");

            // Request shared signals (shared=true) instead of all signals
            var signalEntities = await _connection.Signals.ListAsync(shared: true);
            var signals = new List<SeqSignal>();

            foreach (var signalEntity in signalEntities)
            {
                var signal = new SeqSignal(
                    Id: signalEntity.Id ?? string.Empty,
                    Title: signalEntity.Title ?? "Untitled",
                    Description: signalEntity.Description,
                    Filter: signalEntity.Filters?.FirstOrDefault()?.Filter
                );
                signals.Add(signal);
            }

            _logger.LogInformation(
                "Found {SignalCount} signals",
                signals.Count);

            return new ListSignalsResult(signals, signals.Count);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while listing signals: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while listing signals");
            throw;
        }
    }

    public async Task<ExecuteSqlResult> ExecuteSqlAsync(string query)
    {
        if (query == null)
        {
            _logger.LogError("ExecuteSqlAsync called with null query");
            throw new ArgumentException(
                "Query cannot be null",
                nameof(query));
        }

        try
        {
            _logger.LogInformation(
                "Executing SQL query: {Query}",
                query);

            var result = await _connection.Data.QueryAsync(query);
            var resultJson = System.Text.Json.JsonSerializer.Serialize(result);

            var rowCount = 0;
            if (result != null && result.Rows != null)
            {
                rowCount = result.Rows.Count();
            }

            _logger.LogInformation(
                "SQL query executed successfully, returned {RowCount} rows",
                rowCount);

            return new ExecuteSqlResult(
                Query: query,
                Result: resultJson,
                RowCount: rowCount);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while executing SQL query: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while executing SQL query");
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        // HttpClient is now a singleton managed by DI container, do not dispose
    }
}
