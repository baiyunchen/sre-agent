using System.Text.Json;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface IAuditService
{
    Task LogAsync(Guid sessionId, string eventType, string? description, object? data = null,
        string? actor = null, string? actorId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly IAuditLogRepository _repository;

    public AuditService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task LogAsync(Guid sessionId, string eventType, string? description, object? data = null,
        string? actor = null, string? actorId = null, CancellationToken ct = default)
    {
        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            EventType = eventType,
            EventDescription = description,
            EventData = data != null ? JsonSerializer.SerializeToDocument(data) : null,
            Actor = actor,
            ActorId = actorId,
            OccurredAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(auditLog, ct);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _repository.GetBySessionAsync(sessionId, ct);
    }
}
