using System.Text.Json;

namespace SreAgent.Repository.Entities;

public class InterventionEntity
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public JsonDocument? Data { get; set; }

    public string? IntervenedBy { get; set; }
    public DateTime IntervenedAt { get; set; } = DateTime.UtcNow;

    public SessionEntity Session { get; set; } = null!;
}
