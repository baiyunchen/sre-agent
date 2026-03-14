using SreAgent.Framework.Contexts;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface ISessionRecoveryService
{
    Task<bool> CanResumeAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Prepares session for resume: restores from checkpoint, adds optional input, updates status to Running.
    /// Returns the prepared context for the caller to start background execution.
    /// </summary>
    Task<IContextManager> PrepareResumeAsync(
        Guid sessionId, string? continueInput = null, CancellationToken ct = default);
}

public class SessionRecoveryService : ISessionRecoveryService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ICheckpointService _checkpointService;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ContextManagerOptions _contextOptions;
    private readonly IAuditService _auditService;

    public SessionRecoveryService(
        ISessionRepository sessionRepository,
        ICheckpointService checkpointService,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions contextOptions,
        IAuditService auditService)
    {
        _sessionRepository = sessionRepository;
        _checkpointService = checkpointService;
        _tokenEstimator = tokenEstimator;
        _contextOptions = contextOptions;
        _auditService = auditService;
    }

    public async Task<bool> CanResumeAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct);
        return session?.Status is "Interrupted" or "WaitingApproval";
    }

    public async Task<IContextManager> PrepareResumeAsync(
        Guid sessionId, string? continueInput = null, CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"Session {sessionId} not found");

        if (!await CanResumeAsync(sessionId, ct))
            throw new InvalidOperationException(
                $"Session {sessionId} cannot be resumed from status {session.Status}");

        var checkpoint = await _checkpointService.GetLatestCheckpointAsync(sessionId, ct)
            ?? throw new InvalidOperationException($"No checkpoint found for session {sessionId}");

        var context = await _checkpointService.RestoreFromCheckpointAsync(
            checkpoint.Id, _tokenEstimator, _contextOptions, ct);

        if (!string.IsNullOrEmpty(continueInput))
        {
            context.AddUserMessage(continueInput);
        }

        session.Status = "Running";
        await _sessionRepository.UpdateAsync(session, ct);

        await _auditService.LogAsync(sessionId, "SessionResumed",
            $"Session resumed from checkpoint {checkpoint.CheckpointName}",
            new { checkpointId = checkpoint.Id, continueInput },
            "system", null, ct);

        return context;
    }
}
