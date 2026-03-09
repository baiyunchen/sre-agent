using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IInterventionRepository
{
    Task<InterventionEntity> CreateAsync(InterventionEntity intervention, CancellationToken ct = default);
    Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
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
}
