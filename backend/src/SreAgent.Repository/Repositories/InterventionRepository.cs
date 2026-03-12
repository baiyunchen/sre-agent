using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IInterventionRepository
{
    Task<InterventionEntity> CreateAsync(InterventionEntity intervention, CancellationToken ct = default);
    Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<(IReadOnlyList<InterventionEntity> Items, int Total)> GetByTypesAsync(
        IReadOnlyCollection<string> types,
        int limit = 50,
        CancellationToken ct = default);
}

public class InterventionRepository : IInterventionRepository
{
    private readonly AppDbContext _context;

    public InterventionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<InterventionEntity> CreateAsync(InterventionEntity intervention, CancellationToken ct = default)
    {
        _context.Interventions.Add(intervention);
        await _context.SaveChangesAsync(ct);
        return intervention;
    }

    public async Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.Interventions
            .Where(i => i.SessionId == sessionId)
            .OrderByDescending(i => i.IntervenedAt)
            .ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<InterventionEntity> Items, int Total)> GetByTypesAsync(
        IReadOnlyCollection<string> types,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (types.Count == 0)
            return ([], 0);

        var normalizedLimit = Math.Clamp(limit, 1, 200);
        var query = _context.Interventions
            .AsNoTracking()
            .Where(i => types.Contains(i.Type))
            .OrderByDescending(i => i.IntervenedAt);

        var total = await query.CountAsync(ct);
        var items = await query.Take(normalizedLimit).ToListAsync(ct);
        return (items, total);
    }
}
