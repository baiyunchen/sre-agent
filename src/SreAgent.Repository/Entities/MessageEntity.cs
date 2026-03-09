using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class MessageEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// List of MessagePart serialized as JSONB.
    /// Contains TextPart, ToolCallPart, ToolResultPart, ImagePart, ErrorPart.
    /// </summary>
    public JsonDocument Parts { get; set; } = null!;

    /// <summary>
    /// MessageMetadata serialized as JSONB.
    /// Contains AgentId, EstimatedTokens, Priority, IsDeletable, Tags.
    /// </summary>
    public JsonDocument? Metadata { get; set; }

    public int EstimatedTokens { get; set; }
    public string? AgentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SessionEntity Session { get; set; } = null!;
}
