using Microsoft.EntityFrameworkCore;
using SreAgent.Repository.Entities;

namespace SreAgent.Repository.Repositories;

public interface IApprovalRuleRepository
{
    Task<IReadOnlyList<ApprovalRuleEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ApprovalRuleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApprovalRuleEntity?> GetByToolNameAsync(string toolName, CancellationToken ct = default);
    Task<ApprovalRuleEntity> CreateAsync(ApprovalRuleEntity rule, CancellationToken ct = default);
    Task<ApprovalRuleEntity> UpsertByToolNameAsync(string toolName, string ruleType, string? createdBy, CancellationToken ct = default);
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

        var normalizedToolName = toolName.Trim();
        var normalizedToolNameLower = normalizedToolName.ToLowerInvariant();

        return await _context.ApprovalRules
            .AsNoTracking()
            .Where(r => r.ToolName.ToLower() == normalizedToolNameLower)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApprovalRuleEntity> CreateAsync(ApprovalRuleEntity rule, CancellationToken ct = default)
    {
        _context.ApprovalRules.Add(rule);
        await _context.SaveChangesAsync(ct);
        return rule;
    }

    public async Task<ApprovalRuleEntity> UpsertByToolNameAsync(
        string toolName,
        string ruleType,
        string? createdBy,
        CancellationToken ct = default)
    {
        var normalizedToolName = toolName.Trim();
        var normalizedToolNameLower = normalizedToolName.ToLowerInvariant();
        var normalizedCreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim();
        var utcNow = DateTime.UtcNow;

        var existing = await _context.ApprovalRules
            .Where(r => r.ToolName.ToLower() == normalizedToolNameLower)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (existing is null)
        {
            var created = new ApprovalRuleEntity
            {
                Id = Guid.NewGuid(),
                ToolName = normalizedToolName,
                RuleType = ruleType,
                CreatedBy = normalizedCreatedBy,
                CreatedAt = utcNow
            };
            _context.ApprovalRules.Add(created);
            await _context.SaveChangesAsync(ct);
            return created;
        }

        existing.RuleType = ruleType;
        existing.CreatedBy = normalizedCreatedBy;
        existing.CreatedAt = utcNow;
        await _context.SaveChangesAsync(ct);
        return existing;
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
