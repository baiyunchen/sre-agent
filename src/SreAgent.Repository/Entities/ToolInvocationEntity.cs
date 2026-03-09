using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class ToolInvocationEntity
{
    public Guid Id { get; set; }
    public Guid AgentRunId { get; set; }

    public string ToolName { get; set; } = string.Empty;
    public JsonDocument? Parameters { get; set; }
    public JsonDocument? Result { get; set; }

    public string? ApprovalStatus { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExecutedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }

    public AgentRunEntity AgentRun { get; set; } = null!;
}
