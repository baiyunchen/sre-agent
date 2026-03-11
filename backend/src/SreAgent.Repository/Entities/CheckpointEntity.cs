using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class CheckpointEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string? CheckpointName { get; set; }
    public int StepNumber { get; set; }

    public string? SystemMessage { get; set; }

    /// <summary>
    /// List of message IDs referencing the messages table, stored as JSONB.
    /// </summary>
    public JsonDocument MessageIds { get; set; } = null!;

    public JsonDocument? SessionState { get; set; }
    public JsonDocument? AgentState { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SessionEntity Session { get; set; } = null!;
}
