using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IApprovalRuleRepository
{
    Task<IReadOnlyList<ApprovalRuleEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ApprovalRuleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApprovalRuleEntity?> GetByToolNameAsync(string toolName, CancellationToken ct = default);
    Task<ApprovalRuleEntity> CreateAsync(ApprovalRuleEntity rule, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}

public class ApprovalRuleRepository : IApprovalRuleRepository
{
    private readonly AppDbContext _context;

    public ApprovalRuleRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ApprovalRuleEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await _context.ApprovalRules
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ApprovalRuleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.ApprovalRules
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<ApprovalRuleEntity?> GetByToolNameAsync(string toolName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return null;

        return await _context.ApprovalRules
            .AsNoTracking()
            .Where(r => r.ToolName == toolName.Trim())
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApprovalRuleEntity> CreateAsync(ApprovalRuleEntity rule, CancellationToken ct = default)
    {
        _context.ApprovalRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var rule = await _context.ApprovalRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return false;

        _context.ApprovalRules.Remove(rule);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
