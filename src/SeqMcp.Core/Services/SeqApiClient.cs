using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeqMcp.Core.Configuration;
using SeqMcp.Core.Models;
using System.Net;
using System.Text.Json;

namespace SeqMcp.Core.Services;

/// <summary>
/// Seq API consumer. Owns no HTTP / SeqConnection resources directly — each
/// public method acquires a short-lived <see cref="IConnectionLease"/> from
/// <see cref="ISeqConnectionFactory"/> via <see cref="ResolveEndpoint"/>.
///
/// In PR-3 the endpoint always comes from <see cref="SeqOptions"/> with
/// <see cref="TrustMode.TrustedConfig"/> (single Seq per process). PR-5
/// will let HTTP headers override the endpoint.
/// </summary>
public class SeqApiClient : ISeqApiClient
{
    private readonly ISeqConnectionFactory _factory;
    private readonly SeqOptions _options;
    private readonly ILogger<SeqApiClient> _logger;
    private readonly SeqRequestContext? _requestContext;

    public SeqApiClient(
        ISeqConnectionFactory factory,
        IOptions<SeqOptions> options,
        ILogger<SeqApiClient> logger,
        SeqRequestContext? requestContext = null)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        if (options is null) throw new ArgumentNullException(nameof(options));
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _requestContext = requestContext;
    }

    private SeqEndpoint ResolveEndpoint()
    {
        // PR-5: if SeqRequestContext carries a per-request URL, the
        // middleware has already validated it (scheme/credentials/fragment
        // /control-chars) AND verified that SeqOptions.AllowUrlOverride is
        // on — no need to re-check here. Producing HeaderOverride flips on
        // the SsrfConnectFilter in SeqConnectionFactory.
        var ctxUrl = _requestContext?.SeqUrl;
        var ctxKey = _requestContext?.ApiKey;

        if (!string.IsNullOrWhiteSpace(ctxUrl))
        {
            return new SeqEndpoint(
                ctxUrl,
                ctxKey ?? _options.ApiKey,
                TrustMode.HeaderOverride);
        }

        return new SeqEndpoint(_options.Url, _options.ApiKey, TrustMode.TrustedConfig);
    }

    public async Task<SearchEventsResult> SearchEventsAsync(
        string filter,
        int limit = 100)
    {
        if (filter is null)
        {
            _logger.LogError("SearchEventsAsync called with null filter");
            throw new ArgumentException("Filter cannot be null", nameof(filter));
        }
        if (limit < 0)
        {
            _logger.LogError("SearchEventsAsync called with negative limit: {Limit}", limit);
            throw new ArgumentException("Limit must be non-negative", nameof(limit));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            var combinedFilter = BuildFilterWithScope(filter);
            _logger.LogInformation(
                "Searching events with filter: '{Filter}', limit: {Limit}",
                combinedFilter, limit);

            var url = BuildSearchUrl(combinedFilter, limit);
            _logger.LogInformation("Requesting: {Url}", url);

            var response = await lease.HttpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            return ParseSearchResponse(content);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while searching events: {Message}", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while searching events");
            throw;
        }
    }

    private static string BuildSearchUrl(string combinedFilter, int limit)
    {
        var queryParams = new List<string> { $"count={limit}", "render=true" };
        if (!string.IsNullOrEmpty(combinedFilter))
        {
            queryParams.Add($"filter={WebUtility.UrlEncode(combinedFilter)}");
        }
        return $"/api/events?{string.Join("&", queryParams)}";
    }

    private SearchEventsResult ParseSearchResponse(string content)
    {
        _logger.LogInformation("Seq API response: {Content}", content);
        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var events = new List<SeqEvent>();

        if (root.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("Expected array from Seq API, got {Type}", root.ValueKind);
            return new SearchEventsResult(events, 0);
        }

        foreach (var evt in root.EnumerateArray())
        {
            events.Add(new SeqEvent(
                Id: evt.TryGetProperty("Id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Timestamp: evt.TryGetProperty("Timestamp", out var ts) ? ts.GetString() ?? string.Empty : string.Empty,
                Level: evt.TryGetProperty("Level", out var lvl) ? lvl.GetString() ?? "Information" : "Information",
                RenderedMessage: evt.TryGetProperty("RenderedMessage", out var msg) ? msg.GetString() : null,
                Exception: evt.TryGetProperty("Exception", out var ex) ? ex.GetString() : null));
        }

        _logger.LogInformation("Found {EventCount} events", events.Count);
        return new SearchEventsResult(events, events.Count);
    }

    public async Task<ListSignalsResult> ListSignalsAsync()
    {
        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation("Listing signals");
            var signalEntities = await lease.SeqConnection.Signals.ListAsync(shared: true);
            var signals = new List<SeqSignal>();

            foreach (var signalEntity in signalEntities)
            {
                signals.Add(new SeqSignal(
                    Id: signalEntity.Id ?? string.Empty,
                    Title: signalEntity.Title ?? "Untitled",
                    Description: signalEntity.Description,
                    Filter: signalEntity.Filters?.FirstOrDefault()?.Filter));
            }

            _logger.LogInformation("Found {SignalCount} signals", signals.Count);
            return new ListSignalsResult(signals, signals.Count);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while listing signals: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while listing signals");
            throw;
        }
    }

    public async Task<ExecuteSqlResult> ExecuteSqlAsync(string query)
    {
        if (query is null)
        {
            _logger.LogError("ExecuteSqlAsync called with null query");
            throw new ArgumentException("Query cannot be null", nameof(query));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation("Executing SQL query: {Query}", query);
            var result = await lease.SeqConnection.Data.QueryAsync(query);
            var resultJson = JsonSerializer.Serialize(result);
            var rowCount = result?.Rows?.Count() ?? 0;

            _logger.LogInformation(
                "SQL query executed successfully, returned {RowCount} rows", rowCount);
            return new ExecuteSqlResult(Query: query, Result: resultJson, RowCount: rowCount);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while executing SQL query: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while executing SQL query");
            throw;
        }
    }

    /// <summary>
    /// Builds combined filter with scope filtering.
    /// Priority: HTTP header → ENV var → appsettings.json → none
    /// </summary>
    private string BuildFilterWithScope(string userFilter)
    {
        var projectScope = _requestContext?.ProjectScope ?? _options.ProjectScope;
        if (string.IsNullOrWhiteSpace(projectScope))
        {
            return userFilter;
        }

        var scopeField = _requestContext?.ScopeField ?? _options.ScopeField;
        var scopeFilter = $"{scopeField} = '{projectScope}'";
        _logger.LogInformation("Applying scope filter: {ScopeFilter}", scopeFilter);

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
            throw new ArgumentException("Title cannot be null or empty", nameof(title));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation(
                "Creating signal with title: '{Title}', protected: {IsProtected}",
                title, isProtected);

            var signalEntity = await lease.SeqConnection.Signals.TemplateAsync();
            signalEntity.Title = title;
            signalEntity.Description = description;
            signalEntity.IsProtected = isProtected;

            if (!string.IsNullOrWhiteSpace(filter))
            {
                var filterPart = signalEntity.Filters?.FirstOrDefault();
                if (filterPart != null)
                {
                    filterPart.Filter = filter;
                }
            }

            var createdSignal = await lease.SeqConnection.Signals.AddAsync(signalEntity);
            _logger.LogInformation(
                "Signal created successfully with ID: {SignalId}", createdSignal.Id);

            return new CreateSignalResult(
                SignalId: createdSignal.Id ?? string.Empty,
                Title: title,
                Message: $"Signal '{title}' created successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while creating signal: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while creating signal");
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
            throw new ArgumentException("SignalId cannot be null or empty", nameof(signalId));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation("Updating signal: {SignalId}", signalId);
            var signal = await lease.SeqConnection.Signals.FindAsync(signalId);
            if (signal is null)
            {
                var message = $"Signal with ID '{signalId}' not found";
                _logger.LogWarning(message);
                throw new ArgumentException(message, nameof(signalId));
            }

            if (title != null) signal.Title = title;
            if (description != null) signal.Description = description;
            if (filter != null)
            {
                var filterPart = signal.Filters?.FirstOrDefault();
                if (filterPart != null) filterPart.Filter = filter;
            }

            await lease.SeqConnection.Signals.UpdateAsync(signal);
            _logger.LogInformation("Signal {SignalId} updated successfully", signalId);

            return new UpdateSignalResult(
                SignalId: signalId,
                Message: $"Signal '{signalId}' updated successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while updating signal: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while updating signal");
            throw;
        }
    }

    public async Task<DeleteSignalResult> DeleteSignalAsync(string signalId)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            _logger.LogError("DeleteSignalAsync called with null or empty signalId");
            throw new ArgumentException("SignalId cannot be null or empty", nameof(signalId));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation("Deleting signal: {SignalId}", signalId);
            var signal = await lease.SeqConnection.Signals.FindAsync(signalId);
            if (signal is null)
            {
                var message = $"Signal with ID '{signalId}' not found";
                _logger.LogWarning(message);
                throw new ArgumentException(message, nameof(signalId));
            }

            await lease.SeqConnection.Signals.RemoveAsync(signal);
            _logger.LogInformation("Signal {SignalId} deleted successfully", signalId);

            return new DeleteSignalResult(
                SignalId: signalId,
                Message: $"Signal '{signalId}' deleted successfully");
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while deleting signal: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while deleting signal");
            throw;
        }
    }

    public async Task<GetApplicationsResult> GetApplicationsAsync(int limit = 50)
    {
        if (limit < 0)
        {
            _logger.LogError("GetApplicationsAsync called with negative limit: {Limit}", limit);
            throw new ArgumentException("Limit must be non-negative", nameof(limit));
        }

        await using var lease = _factory.GetConnection(ResolveEndpoint());
        try
        {
            _logger.LogInformation("Getting applications list with limit: {Limit}", limit);
            var scopeField = _requestContext?.ScopeField ?? _options.ScopeField;
            var sqlQuery = $@"
                select {scopeField} as Application, count(*) as EventCount
                from stream
                where {scopeField} is not null
                group by {scopeField}
                order by EventCount desc
                limit {limit}";

            var result = await lease.SeqConnection.Data.QueryAsync(sqlQuery);
            var applications = ExtractApplications(result);

            _logger.LogInformation("Found {ApplicationCount} applications", applications.Count);
            return new GetApplicationsResult(
                Applications: applications, TotalCount: applications.Count);
        }
        catch (Seq.Api.Client.SeqApiException ex)
        {
            _logger.LogError(ex,
                "Seq API error while getting applications: {StatusCode} - {Message}",
                ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while getting applications");
            throw;
        }
    }

    private static List<SeqApplication> ExtractApplications(Seq.Api.Model.Data.QueryResultPart? result)
    {
        var applications = new List<SeqApplication>();
        if (result?.Rows is null) return applications;
        foreach (var row in result.Rows)
        {
            if (row.Length < 2) continue;
            var appName = row[0]?.ToString() ?? "Unknown";
            var eventCount = row[1] != null ? Convert.ToInt32(row[1]) : 0;
            if (appName != "Unknown" && !string.IsNullOrWhiteSpace(appName))
            {
                applications.Add(new SeqApplication(Name: appName, EventCount: eventCount));
            }
        }
        return applications;
    }
}
