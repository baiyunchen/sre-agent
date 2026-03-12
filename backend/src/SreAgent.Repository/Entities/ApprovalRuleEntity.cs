namespace SreAgent.Repository.Entities;

public class ApprovalRuleEntity
{
    public Guid Id { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty; // "always-allow" | "always-deny"
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
