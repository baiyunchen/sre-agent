using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IAgentRunRepository
{
    Task<AgentRunEntity> CreateAsync(AgentRunEntity agentRun, CancellationToken ct = default);
    Task UpdateAsync(AgentRunEntity agentRun, CancellationToken ct = default);
    Task<AgentRunEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
    Task<Dictionary<Guid, int>> CountToolInvocationsBySessionsAsync(IEnumerable<Guid> sessionIds, CancellationToken ct = default);
}

public class AgentRunRepository : IAgentRunRepository
{
    private readonly AppDbContext _context;

    public AgentRunRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<AgentRunEntity> CreateAsync(AgentRunEntity agentRun, CancellationToken ct = default)
    {
        _context.AgentRuns.Add(agentRun);
        await _context.SaveChangesAsync(ct);
        return agentRun;
    }

    public async Task UpdateAsync(AgentRunEntity agentRun, CancellationToken ct = default)
    {
        _context.AgentRuns.Update(agentRun);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<AgentRunEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.AgentRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<IReadOnlyList<AgentRunEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.AgentRuns
            .Where(r => r.SessionId == sessionId)
            .Include(r => r.ToolInvocations)
            .OrderBy(r => r.StartedAt)
            .ToListAsync(ct);
    }

    public async Task<Dictionary<Guid, int>> CountToolInvocationsBySessionsAsync(
        IEnumerable<Guid> sessionIds, CancellationToken ct = default)
    {
        var ids = sessionIds.ToList();
        if (ids.Count == 0) return new Dictionary<Guid, int>();

        return await _context.AgentRuns
            .Where(r => ids.Contains(r.SessionId))
            .SelectMany(r => r.ToolInvocations, (run, _) => run.SessionId)
            .GroupBy(sid => sid)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
    }
}
