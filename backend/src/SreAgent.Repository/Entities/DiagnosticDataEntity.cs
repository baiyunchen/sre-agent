using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class DiagnosticDataEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string SourceType { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public Guid? ToolInvocationId { get; set; }

    public DateTime? LogTimestamp { get; set; }
    public string Content { get; set; } = string.Empty;
    public JsonDocument? StructuredFields { get; set; }
    public string? Severity { get; set; }
    public List<string> Tags { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public SessionEntity Session { get; set; } = null!;
}
