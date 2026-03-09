using System.Text.Json;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface IInterventionService
{
    Task InterruptSessionAsync(Guid sessionId, string reason, string userId, CancellationToken ct = default);
    Task CancelSessionAsync(Guid sessionId, string reason, string userId, CancellationToken ct = default);
    Task ProvideInputAsync(Guid sessionId, JsonDocument input, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default);
}

public class InterventionService : IInterventionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IInterventionRepository _interventionRepository;

    public InterventionService(
        ISessionRepository sessionRepository,
        IInterventionRepository interventionRepository)
    {
        _sessionRepository = sessionRepository;
        _interventionRepository = interventionRepository;
    }

    public async Task InterruptSessionAsync(Guid sessionId, string reason, string userId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        if (session.Status != "Running")
            throw new InvalidOperationException($"Can only interrupt running sessions, current status: {session.Status}");

        session.Status = "Interrupted";
        await _sessionRepository.UpdateAsync(session, ct);

        await _interventionRepository.CreateAsync(new InterventionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = "Interrupt",
            Reason = reason,
            IntervenedBy = userId,
            IntervenedAt = DateTime.UtcNow
        }, ct);
    }

    public async Task CancelSessionAsync(Guid sessionId, string reason, string userId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        session.Status = "Cancelled";
        await _sessionRepository.UpdateAsync(session, ct);

        await _interventionRepository.CreateAsync(new InterventionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = "Cancel",
            Reason = reason,
            IntervenedBy = userId,
            IntervenedAt = DateTime.UtcNow
        }, ct);
    }

    public async Task ProvideInputAsync(Guid sessionId, JsonDocument input, string userId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        await _interventionRepository.CreateAsync(new InterventionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = "ProvideInput",
            Data = input,
            IntervenedBy = userId,
            IntervenedAt = DateTime.UtcNow
        }, ct);
    }

    public async Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _interventionRepository.GetBySessionAsync(sessionId, ct);
    }
}
