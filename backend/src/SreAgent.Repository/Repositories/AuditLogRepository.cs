using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IAuditLogRepository
{
    Task<AuditLogEntity> CreateAsync(AuditLogEntity auditLog, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntity>> GetByEventTypeAsync(string eventType, int limit = 100, CancellationToken ct = default);
    Task<(IReadOnlyList<AuditLogEntity> Items, int Total)> GetRecentAsync(int limit = 20, CancellationToken ct = default);
}

public class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AuditLogEntity> CreateAsync(AuditLogEntity auditLog, CancellationToken ct = default)
    {
        _context.AuditLogs.Add(auditLog);
        await _context.SaveChangesAsync(ct);
        return auditLog;
    }

    public async Task<IReadOnlyList<AuditLogEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(a => a.SessionId == sessionId)
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> GetByEventTypeAsync(string eventType, int limit = 100, CancellationToken ct = default)
    {
        return await _context.AuditLogs
            .Where(a => a.EventType == eventType)
            .OrderByDescending(a => a.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditLogEntity> Items, int Total)> GetRecentAsync(int limit = 20, CancellationToken ct = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var query = _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(a => a.OccurredAt);

        var total = await query.CountAsync(ct);
        var items = await query.Take(normalizedLimit).ToListAsync(ct);
        return (items, total);
    }
}
