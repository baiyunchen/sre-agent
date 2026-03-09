using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IAgentRunRepository
{
    Task<AgentRunEntity> CreateAsync(AgentRunEntity agentRun, CancellationToken ct = default);
    Task UpdateAsync(AgentRunEntity agentRun, CancellationToken ct = default);
    Task<IReadOnlyList<AgentRunEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
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

    public async Task<IReadOnlyList<AgentRunEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.AgentRuns
            .Where(r => r.SessionId == sessionId)
            .Include(r => r.ToolInvocations)
            .OrderBy(r => r.StartedAt)
            .ToListAsync(ct);
    }
}
