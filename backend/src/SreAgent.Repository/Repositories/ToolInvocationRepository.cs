using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IToolInvocationRepository
{
    Task<ToolInvocationEntity> CreateAsync(ToolInvocationEntity invocation, CancellationToken ct = default);
    Task UpdateAsync(ToolInvocationEntity invocation, CancellationToken ct = default);
    Task<ToolInvocationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ToolInvocationEntity>> GetByAgentRunAsync(Guid agentRunId, CancellationToken ct = default);
}

public class ToolInvocationRepository : IToolInvocationRepository
{
    private readonly AppDbContext _context;

    public ToolInvocationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ToolInvocationEntity> CreateAsync(ToolInvocationEntity invocation, CancellationToken ct = default)
    {
        _context.ToolInvocations.Add(invocation);
        await _context.SaveChangesAsync(ct);
        return invocation;
    }

    public async Task UpdateAsync(ToolInvocationEntity invocation, CancellationToken ct = default)
    {
        _context.ToolInvocations.Update(invocation);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<ToolInvocationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ToolInvocations
            .Include(i => i.AgentRun)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<IReadOnlyList<ToolInvocationEntity>> GetByAgentRunAsync(Guid agentRunId, CancellationToken ct = default)
    {
        return await _context.ToolInvocations
            .Where(i => i.AgentRunId == agentRunId)
            .OrderBy(i => i.RequestedAt)
            .ToListAsync(ct);
    }
}
