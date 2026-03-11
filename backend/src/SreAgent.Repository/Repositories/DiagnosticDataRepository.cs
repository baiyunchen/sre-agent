using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IDiagnosticDataRepository
{
    Task AddRangeAsync(IEnumerable<DiagnosticDataEntity> records, CancellationToken ct = default);
    Task<IReadOnlyList<DiagnosticDataEntity>> SearchAsync(
        Guid sessionId, string? keyword, string? severity, string? sourceType,
        DateTime? startTime, DateTime? endTime, int limit, CancellationToken ct = default);
    Task<DiagnosticSummaryResult> GetSummaryAsync(Guid sessionId, string? sourceType, CancellationToken ct = default);
    Task<int> DeleteExpiredAsync(CancellationToken ct = default);
}

public class DiagnosticSummaryResult
{
    public int TotalRecords { get; set; }
    public Dictionary<string, int> BySeverity { get; set; } = new();
    public Dictionary<string, int> BySource { get; set; } = new();
    public DateTime? EarliestTimestamp { get; set; }
    public DateTime? LatestTimestamp { get; set; }
}

public class DiagnosticDataRepository : IDiagnosticDataRepository
{
    private readonly AppDbContext _context;

    public DiagnosticDataRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<DiagnosticDataEntity> records, CancellationToken ct = default)
    {
        _context.DiagnosticData.AddRange(records);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DiagnosticDataEntity>> SearchAsync(
        Guid sessionId, string? keyword, string? severity, string? sourceType,
        DateTime? startTime, DateTime? endTime, int limit, CancellationToken ct = default)
    {
        var query = _context.DiagnosticData
            .Where(d => d.SessionId == sessionId);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(d => d.Severity == severity);

        if (!string.IsNullOrWhiteSpace(sourceType))
            query = query.Where(d => d.SourceType == sourceType);

        if (startTime.HasValue)
            query = query.Where(d => d.LogTimestamp >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(d => d.LogTimestamp <= endTime.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(d => d.Content.Contains(keyword));

        return await query
            .OrderByDescending(d => d.LogTimestamp ?? d.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<DiagnosticSummaryResult> GetSummaryAsync(
        Guid sessionId, string? sourceType, CancellationToken ct = default)
    {
        var query = _context.DiagnosticData.Where(d => d.SessionId == sessionId);
        if (!string.IsNullOrWhiteSpace(sourceType))
            query = query.Where(d => d.SourceType == sourceType);

        var data = await query.ToListAsync(ct);
        if (data.Count == 0)
            return new DiagnosticSummaryResult();

        return new DiagnosticSummaryResult
        {
            TotalRecords = data.Count,
            BySeverity = data
                .Where(d => d.Severity != null)
                .GroupBy(d => d.Severity!)
                .ToDictionary(g => g.Key, g => g.Count()),
            BySource = data
                .GroupBy(d => d.SourceType)
                .ToDictionary(g => g.Key, g => g.Count()),
            EarliestTimestamp = data.Where(d => d.LogTimestamp.HasValue).MinBy(d => d.LogTimestamp)?.LogTimestamp,
            LatestTimestamp = data.Where(d => d.LogTimestamp.HasValue).MaxBy(d => d.LogTimestamp)?.LogTimestamp
        };
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        return await _context.DiagnosticData
            .Where(d => d.ExpiresAt < DateTime.UtcNow)
            .ExecuteDeleteAsync(ct);
    }
}
