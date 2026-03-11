using System.Text.Json;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface IDiagnosticDataService
{
    Task<int> StoreBatchAsync(Guid sessionId, IEnumerable<DiagnosticDataInput> records, CancellationToken ct = default);
    Task<DiagnosticSearchResult> SearchAsync(DiagnosticSearchRequest request, CancellationToken ct = default);
    Task<DiagnosticSummaryResult> GetSummaryAsync(Guid sessionId, string? sourceType = null, CancellationToken ct = default);
}

public class DiagnosticDataInput
{
    public string SourceType { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public Guid? ToolInvocationId { get; set; }
    public DateTime? LogTimestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? StructuredFields { get; set; }
    public string? Severity { get; set; }
    public List<string>? Tags { get; set; }
}

public class DiagnosticSearchRequest
{
    public Guid SessionId { get; set; }
    public string? Keyword { get; set; }
    public string? Severity { get; set; }
    public string? SourceType { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public int Limit { get; set; } = 50;
}

public class DiagnosticSearchResult
{
    public int TotalMatches { get; set; }
    public IReadOnlyList<DiagnosticSearchItem> Results { get; set; } = [];
}

public class DiagnosticSearchItem
{
    public Guid Id { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public DateTime? LogTimestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Severity { get; set; }
}

public class DiagnosticDataService : IDiagnosticDataService
{
    private readonly IDiagnosticDataRepository _repository;
    private const int DefaultTtlDays = 7;

    public DiagnosticDataService(IDiagnosticDataRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> StoreBatchAsync(Guid sessionId, IEnumerable<DiagnosticDataInput> records, CancellationToken ct = default)
    {
        var entities = records.Select(r => new DiagnosticDataEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            SourceType = r.SourceType,
            SourceName = r.SourceName,
            ToolInvocationId = r.ToolInvocationId,
            LogTimestamp = r.LogTimestamp,
            Content = r.Content,
            StructuredFields = r.StructuredFields != null
                ? JsonSerializer.SerializeToDocument(r.StructuredFields)
                : null,
            Severity = r.Severity,
            Tags = r.Tags ?? [],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(DefaultTtlDays)
        }).ToList();

        await _repository.AddRangeAsync(entities, ct);
        return entities.Count;
    }

    public async Task<DiagnosticSearchResult> SearchAsync(DiagnosticSearchRequest request, CancellationToken ct = default)
    {
        var results = await _repository.SearchAsync(
            request.SessionId, request.Keyword, request.Severity,
            request.SourceType, request.StartTime, request.EndTime,
            request.Limit, ct);

        return new DiagnosticSearchResult
        {
            TotalMatches = results.Count,
            Results = results.Select(d => new DiagnosticSearchItem
            {
                Id = d.Id,
                SourceType = d.SourceType,
                SourceName = d.SourceName,
                LogTimestamp = d.LogTimestamp,
                Content = d.Content,
                Severity = d.Severity
            }).ToList()
        };
    }

    public async Task<DiagnosticSummaryResult> GetSummaryAsync(Guid sessionId, string? sourceType = null, CancellationToken ct = default)
    {
        return await _repository.GetSummaryAsync(sessionId, sourceType, ct);
    }
}
