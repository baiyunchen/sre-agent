using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class SessionEntity
{
    public Guid Id { get; set; }
    public string Status { get; set; } = "Created";

    public string? AlertId { get; set; }
    public string? AlertName { get; set; }
    public string? AlertSource { get; set; }
    public string? AlertSeverity { get; set; }
    public JsonDocument? AlertData { get; set; }

    public string? ServiceName { get; set; }
    public JsonDocument? ServiceMetadata { get; set; }

    public string? CurrentAgentId { get; set; }
    public int CurrentStep { get; set; }
    public JsonDocument? ExecutionState { get; set; }

    public JsonDocument? Diagnosis { get; set; }
    public string? DiagnosisSummary { get; set; }
    public double? Confidence { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<MessageEntity> Messages { get; set; } = [];
    public ICollection<AgentRunEntity> AgentRuns { get; set; } = [];
    public ICollection<CheckpointEntity> Checkpoints { get; set; } = [];
    public ICollection<InterventionEntity> Interventions { get; set; } = [];
    public ICollection<DiagnosticDataEntity> DiagnosticData { get; set; } = [];
    public ICollection<AuditLogEntity> AuditLogs { get; set; } = [];
}
