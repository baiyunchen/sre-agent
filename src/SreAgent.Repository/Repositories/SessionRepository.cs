using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface ISessionRepository
{
    Task<SessionEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<SessionEntity> CreateAsync(SessionEntity session, CancellationToken ct = default);
    Task UpdateAsync(SessionEntity session, CancellationToken ct = default);
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
}
