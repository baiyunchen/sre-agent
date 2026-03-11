using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class AgentRunEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public string? AgentName { get; set; }

    public JsonDocument? Input { get; set; }
    public JsonDocument? Output { get; set; }

    public string Status { get; set; } = "Running";
    public string? ErrorMessage { get; set; }

    public double? Confidence { get; set; }
    public JsonDocument? Finding { get; set; }

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public long DurationMs { get; set; }

    public SessionEntity Session { get; set; } = null!;
    public ICollection<ToolInvocationEntity> ToolInvocations { get; set; } = [];
}
