using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public interface IApprovalService
{
    Task<(IReadOnlyList<SessionEntity> Items, int Total)> GetPendingAsync(int limit, CancellationToken ct = default);
    Task<ApprovalDecisionResult> ApproveAsync(Guid sessionId, string approverId, string? comment, CancellationToken ct = default);
    Task<ApprovalDecisionResult> RejectAsync(Guid sessionId, string approverId, string? comment, CancellationToken ct = default);
    Task<(IReadOnlyList<InterventionEntity> Items, int Total)> GetHistoryAsync(int limit, CancellationToken ct = default);
}

public class ApprovalService : IApprovalService
{
    private static readonly IReadOnlyCollection<string> ApprovalHistoryTypes = ["Approve", "Reject"];

    private readonly ISessionRepository _sessionRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly IAuditService _auditService;

    public ApprovalService(
        ISessionRepository sessionRepository,
        IInterventionRepository interventionRepository,
        IAuditService auditService)
    {
        _sessionRepository = sessionRepository;
        _interventionRepository = interventionRepository;
        _auditService = auditService;
    }

    public async Task<(IReadOnlyList<SessionEntity> Items, int Total)> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 100);
        var allPending = await _sessionRepository.GetByStatusAsync("WaitingApproval", ct);
        var items = allPending.Take(normalizedLimit).ToList();
        return (items, allPending.Count);
    }

    public async Task<ApprovalDecisionResult> ApproveAsync(
        Guid sessionId,
        string approverId,
        string? comment,
        CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found");

        if (!string.Equals(session.Status, "WaitingApproval", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Session status must be WaitingApproval, current: {session.Status}");

        session.Status = "Running";
        await _sessionRepository.UpdateAsync(session, ct);

        await _interventionRepository.CreateAsync(new InterventionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = "Approve",
            Reason = comment,
            IntervenedBy = approverId,
            IntervenedAt = DateTime.UtcNow
        }, ct);

        await _auditService.LogAsync(
            sessionId,
            "SessionApproved",
            $"Session approved by {approverId}",
            new { comment },
            approverId,
            null,
            ct);

        return new ApprovalDecisionResult
        {
            SessionId = sessionId,
            Status = session.Status,
            Message = "Approval accepted"
        };
    }

    public async Task<ApprovalDecisionResult> RejectAsync(
        Guid sessionId,
        string approverId,
        string? comment,
        CancellationToken ct = default)
    {
        var session = await _sessionRepository.GetAsync(sessionId, ct)
            ?? throw new KeyNotFoundException($"Session {sessionId} not found");

        if (!string.Equals(session.Status, "WaitingApproval", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Session status must be WaitingApproval, current: {session.Status}");

        session.Status = "Cancelled";
        await _sessionRepository.UpdateAsync(session, ct);

        await _interventionRepository.CreateAsync(new InterventionEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            Type = "Reject",
            Reason = comment,
            IntervenedBy = approverId,
            IntervenedAt = DateTime.UtcNow
        }, ct);

        await _auditService.LogAsync(
            sessionId,
            "SessionRejected",
            $"Session rejected by {approverId}",
            new { comment },
            approverId,
            null,
            ct);

        return new ApprovalDecisionResult
        {
            SessionId = sessionId,
            Status = session.Status,
            Message = "Rejection accepted"
        };
    }

    public Task<(IReadOnlyList<InterventionEntity> Items, int Total)> GetHistoryAsync(int limit, CancellationToken ct = default)
    {
        return _interventionRepository.GetByTypesAsync(ApprovalHistoryTypes, limit, ct);
    }
}

public class ApprovalDecisionResult
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
