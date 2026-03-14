namespace SreAgent.Application.Services;

/// <summary>
/// Resolves pending tool approvals. Used by the API layer.
/// </summary>
public interface IToolApprovalResolver
{
    Task ResolveApprovalAsync(Guid invocationId, bool approved, string? reason, string? approverId, CancellationToken ct = default);
    bool HasPendingApproval(Guid invocationId);
}
