using System.Text.Json;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
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
    private readonly ICheckpointService _checkpointService;
    private readonly IContextStore _contextStore;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly IAuditService _auditService;

    public InterventionService(
        ISessionRepository sessionRepository,
        IInterventionRepository interventionRepository,
        ICheckpointService checkpointService,
        IContextStore contextStore,
        ITokenEstimator tokenEstimator,
        IAuditService auditService)
    {
        _sessionRepository = sessionRepository;
        _interventionRepository = interventionRepository;
        _checkpointService = checkpointService;
        _contextStore = contextStore;
        _tokenEstimator = tokenEstimator;
        _auditService = auditService;
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

        await TryCreateCheckpointAsync(sessionId, "interrupt", ct);

        await _auditService.LogAsync(sessionId, "SessionInterrupted",
            $"Session interrupted by {userId}: {reason}",
            new { reason }, userId, null, ct);
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

        await _auditService.LogAsync(sessionId, "SessionCancelled",
            $"Session cancelled by {userId}: {reason}",
            new { reason }, userId, null, ct);
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

        await _auditService.LogAsync(sessionId, "InputProvided",
            $"Input provided by {userId}",
            null, userId, null, ct);
    }

    public async Task<IReadOnlyList<InterventionEntity>> GetBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _interventionRepository.GetBySessionAsync(sessionId, ct);
    }

    private async Task TryCreateCheckpointAsync(Guid sessionId, string name, CancellationToken ct)
    {
        try
        {
            var snapshot = await _contextStore.GetAsync(sessionId, ct);
            if (snapshot == null) return;

            var context = DefaultContextManager.FromSnapshot(snapshot, _tokenEstimator);
            await _checkpointService.CreateCheckpointAsync(sessionId, context, name, ct);
        }
        catch
        {
            // Best effort - don't fail the interrupt if checkpoint creation fails
        }
    }
}
