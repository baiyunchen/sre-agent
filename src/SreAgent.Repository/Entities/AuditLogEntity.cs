using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class AuditLogEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string? EventDescription { get; set; }
    public JsonDocument? EventData { get; set; }

    public string? Actor { get; set; }
    public string? ActorId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public SessionEntity Session { get; set; } = null!;
}
