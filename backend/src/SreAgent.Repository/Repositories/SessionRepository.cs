using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;
using System.Text.Json;

namespace SreAgent.Repository.Repositories;

public interface ISessionRepository
{
    Task<SessionEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SessionEntity> CreateAsync(SessionEntity session, CancellationToken ct = default);
    Task UpdateAsync(SessionEntity session, CancellationToken ct = default);
    Task<(IReadOnlyList<SessionEntity> Items, int Total)> ListAsync(SessionListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<SessionEntity>> GetByStatusAsync(string status, CancellationToken ct = default);
    Task<IReadOnlyList<SessionEntity>> GetByAlertAsync(string alertId, CancellationToken ct = default);
}

public class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _context;

    public SessionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SessionEntity?> GetAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<SessionEntity> CreateAsync(SessionEntity session, CancellationToken ct = default)
    {
        _context.Sessions.Add(session);
        await _context.SaveChangesAsync(ct);
        return session;
    }

    public async Task UpdateAsync(SessionEntity session, CancellationToken ct = default)
    {
        session.UpdatedAt = DateTime.UtcNow;
        _context.Sessions.Update(session);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<SessionEntity> Items, int Total)> ListAsync(SessionListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var dataQuery = _context.Sessions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dataQuery = dataQuery.Where(s => s.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLowerInvariant();
            var hasGuid = Guid.TryParse(search, out var sessionIdSearch);

            dataQuery = dataQuery.Where(s =>
                (s.AlertName != null && s.AlertName.ToLower().Contains(search)) ||
                (s.AlertId != null && s.AlertId.ToLower().Contains(search)) ||
                (s.ServiceName != null && s.ServiceName.ToLower().Contains(search)) ||
                (hasGuid && s.Id == sessionIdSearch));
        }

        dataQuery = ApplySort(dataQuery, query.Sort, query.SortOrder);

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            var source = query.Source.Trim();
            var sourceFiltered = (await dataQuery.ToListAsync(ct))
                .Where(s => string.Equals(s.AlertSource, source, StringComparison.OrdinalIgnoreCase)
                            || MatchSource(s.AlertData, source))
                .ToList();

            var sourceTotal = sourceFiltered.Count;
            var sourceItems = sourceFiltered.Skip(skip).Take(pageSize).ToList();
            return (sourceItems, sourceTotal);
        }

        var total = await dataQuery.CountAsync(ct);
        var items = await dataQuery.Skip(skip).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }

    public async Task<IReadOnlyList<SessionEntity>> GetByStatusAsync(string status, CancellationToken ct = default)
    {
        return await _context.Sessions
            .Where(s => s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SessionEntity>> GetByAlertAsync(string alertId, CancellationToken ct = default)
    {
        return await _context.Sessions
            .Where(s => s.AlertId == alertId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    private static IQueryable<SessionEntity> ApplySort(IQueryable<SessionEntity> query, string? sort, string? sortOrder)
    {
        var sortKey = sort?.Trim();
        var isDesc = !string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        return (sortKey, isDesc) switch
        {
            ("updatedAt", true) => query.OrderByDescending(s => s.UpdatedAt),
            ("updatedAt", false) => query.OrderBy(s => s.UpdatedAt),
            ("status", true) => query.OrderByDescending(s => s.Status).ThenByDescending(s => s.CreatedAt),
            ("status", false) => query.OrderBy(s => s.Status).ThenByDescending(s => s.CreatedAt),
            (_, false) => query.OrderBy(s => s.CreatedAt),
            _ => query.OrderByDescending(s => s.CreatedAt)
        };
    }

    private static bool MatchSource(JsonDocument? alertData, string expectedSource)
    {
        if (alertData == null || alertData.RootElement.ValueKind != JsonValueKind.Object)
            return false;

        return TryMatchSourceProperty(alertData.RootElement, "source", expectedSource)
               || TryMatchSourceProperty(alertData.RootElement, "alertSource", expectedSource);
    }

    private static bool TryMatchSourceProperty(JsonElement root, string propertyName, string expectedSource)
    {
        if (!root.TryGetProperty(propertyName, out var sourceElement))
            return false;

        if (sourceElement.ValueKind != JsonValueKind.String)
            return false;

        return string.Equals(sourceElement.GetString(), expectedSource, StringComparison.OrdinalIgnoreCase);
    }
}
