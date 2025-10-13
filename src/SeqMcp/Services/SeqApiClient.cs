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
    private readonly SeqRequestContext? _requestContext;

    public SeqApiClient(
        HttpClient httpClient,
        SeqServerConfig config,
        ILogger<SeqApiClient> logger,
        SeqRequestContext? requestContext = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestContext = requestContext; // Optional: null for tests

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
            // Build combined filter with scope filtering
            var combinedFilter = BuildFilterWithScope(filter);

            _logger.LogInformation(
                "Searching events with filter: '{Filter}', limit: {Limit}",
                combinedFilter,
                limit);

            // Use direct HTTP request to /api/events to avoid WebSocket requirement
            var queryParams = new List<string>
            {
                $"count={limit}",
                "render=true"
            };

            if (!string.IsNullOrEmpty(combinedFilter))
            {
                queryParams.Add($"filter={WebUtility.UrlEncode(combinedFilter)}");
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

    /// <summary>
    /// Builds combined filter with scope filtering.
    /// Priority: HTTP header → ENV var → appsettings.json → none
    /// </summary>
    private string BuildFilterWithScope(string userFilter)
    {
        // Determine project scope: HTTP header → default config → null
        var projectScope = _requestContext?.ProjectScope
                          ?? _config.DefaultProjectScope;

        // If no scope, return user filter as-is
        if (string.IsNullOrWhiteSpace(projectScope))
        {
            return userFilter;
        }

        // Determine scope field: HTTP header → default config
        var scopeField = _requestContext?.ScopeField
                        ?? _config.DefaultScopeField;

        // Build scope filter: ScopeField = 'ProjectScope'
        var scopeFilter = $"{scopeField} = '{projectScope}'";

        _logger.LogInformation(
            "Applying scope filter: {ScopeFilter}",
            scopeFilter);

        // Combine with user filter
        if (string.IsNullOrWhiteSpace(userFilter))
        {
            return scopeFilter;
        }

        return $"({scopeFilter}) and ({userFilter})";
    }

    public async Task<CreateSignalResult> CreateSignalAsync(
        string title,
        string? description,
        string? filter,
        bool isProtected = false)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            _logger.LogError("CreateSignalAsync called with null or empty title");
            throw new ArgumentException(
                "Title cannot be null or empty",
                nameof(title));
        }

        try
        {
            _logger.LogInformation(
                "Creating signal with title: '{Title}', protected: {IsProtected}",
                title,
                isProtected);

            var signalEntity = await _connection.Signals.TemplateAsync();
            signalEntity.Title = title;
            signalEntity.Description = description;
            signalEntity.IsProtected = isProtected;

            // Set filter if provided
            if (!string.IsNullOrWhiteSpace(filter))
            {
                // Create a simple filter using the signal's existing filter structure
                var filterPart = signalEntity.Filters?.FirstOrDefault();
                if (filterPart != null)
                {
                    filterPart.Filter = filter;
                }
            }

            var createdSignal = await _connection.Signals.AddAsync(signalEntity);

            _logger.LogInformation(
                "Signal created successfully with ID: {SignalId}",
                createdSignal.Id);

            return new CreateSignalResult(
                SignalId: createdSignal.Id ?? string.Empty,
                Title: title,
                Message: $"Signal '{title}' created successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while creating signal: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while creating signal");
            throw;
        }
    }

    public async Task<UpdateSignalResult> UpdateSignalAsync(
        string signalId,
        string? title = null,
        string? description = null,
        string? filter = null)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            _logger.LogError("UpdateSignalAsync called with null or empty signalId");
            throw new ArgumentException(
                "SignalId cannot be null or empty",
                nameof(signalId));
        }

        try
        {
            _logger.LogInformation(
                "Updating signal: {SignalId}",
                signalId);

            var signal = await _connection.Signals.FindAsync(signalId);

            if (signal == null)
            {
                var message = $"Signal with ID '{signalId}' not found";
                _logger.LogWarning(message);
                throw new ArgumentException(message, nameof(signalId));
            }

            // Update fields if provided
            if (title != null)
            {
                signal.Title = title;
            }

            if (description != null)
            {
                signal.Description = description;
            }

            if (filter != null)
            {
                var filterPart = signal.Filters?.FirstOrDefault();
                if (filterPart != null)
                {
                    filterPart.Filter = filter;
                }
            }

            await _connection.Signals.UpdateAsync(signal);

            _logger.LogInformation(
                "Signal {SignalId} updated successfully",
                signalId);

            return new UpdateSignalResult(
                SignalId: signalId,
                Message: $"Signal '{signalId}' updated successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while updating signal: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while updating signal");
            throw;
        }
    }

    public async Task<DeleteSignalResult> DeleteSignalAsync(string signalId)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            _logger.LogError("DeleteSignalAsync called with null or empty signalId");
            throw new ArgumentException(
                "SignalId cannot be null or empty",
                nameof(signalId));
        }

        try
        {
            _logger.LogInformation(
                "Deleting signal: {SignalId}",
                signalId);

            var signal = await _connection.Signals.FindAsync(signalId);

            if (signal == null)
            {
                var message = $"Signal with ID '{signalId}' not found";
                _logger.LogWarning(message);
                throw new ArgumentException(message, nameof(signalId));
            }

            await _connection.Signals.RemoveAsync(signal);

            _logger.LogInformation(
                "Signal {SignalId} deleted successfully",
                signalId);

            return new DeleteSignalResult(
                SignalId: signalId,
                Message: $"Signal '{signalId}' deleted successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while deleting signal: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while deleting signal");
            throw;
        }
    }

    public async Task<GetApplicationsResult> GetApplicationsAsync(int limit = 50)
    {
        if (limit < 0)
        {
            _logger.LogError(
                "GetApplicationsAsync called with negative limit: {Limit}",
                limit);
            throw new ArgumentException(
                "Limit must be non-negative",
                nameof(limit));
        }

        try
        {
            _logger.LogInformation(
                "Getting applications list with limit: {Limit}",
                limit);

            // Determine scope field for grouping
            var scopeField = _requestContext?.ScopeField
                            ?? _config.DefaultScopeField;

            // Use SQL query to get unique applications with event counts
            var sqlQuery = $@"
                select {scopeField} as Application, count(*) as EventCount
                from stream
                where {scopeField} is not null
                group by {scopeField}
                order by EventCount desc
                limit {limit}";

            var result = await _connection.Data.QueryAsync(sqlQuery);
            var applications = new List<SeqApplication>();

            if (result?.Rows != null)
            {
                foreach (var row in result.Rows)
                {
                    // SQL query result rows are object arrays matching column order
                    // Our query: select {scopeField} as Application, count(*) as EventCount
                    // So: row[0] = Application, row[1] = EventCount
                    if (row.Length >= 2)
                    {
                        var appName = row[0]?.ToString() ?? "Unknown";
                        var eventCount = row[1] != null ? Convert.ToInt32(row[1]) : 0;

                        if (appName != "Unknown" && !string.IsNullOrWhiteSpace(appName))
                        {
                            applications.Add(new SeqApplication(
                                Name: appName,
                                EventCount: eventCount));
                        }
                    }
                }
            }

            _logger.LogInformation(
                "Found {ApplicationCount} applications",
                applications.Count);

            return new GetApplicationsResult(
                Applications: applications,
                TotalCount: applications.Count);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(
                ex,
                "Seq API error while getting applications: {StatusCode} - {Message}",
                ex.StatusCode,
                ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error while getting applications");
            throw;
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
        // HttpClient is now a singleton managed by DI container, do not dispose
    }
}
