using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

/// <summary>
/// Implements IToolApprovalChecker and IToolApprovalResolver for per-tool approval workflow.
/// Singleton to maintain pending approvals across requests.
/// </summary>
public sealed class ToolApprovalService : IToolApprovalChecker, IToolApprovalResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionStreamPublisher _streamPublisher;
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ToolApprovalResult>> _pendingApprovals = new();

    public ToolApprovalService(
        IServiceScopeFactory scopeFactory,
        ISessionStreamPublisher streamPublisher)
    {
        _scopeFactory = scopeFactory;
        _streamPublisher = streamPublisher;
    }

    public async Task<ToolApprovalCheckResult> CheckRuleAsync(string toolName, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IApprovalRuleRepository>();
        var rule = await repo.GetByToolNameAsync(toolName, ct);
        if (rule == null)
            return new ToolApprovalCheckResult(null, false);

        return rule.RuleType switch
        {
            "always-allow" => new ToolApprovalCheckResult(true, false),
            "always-deny" => new ToolApprovalCheckResult(false, false),
            "require-approval" => new ToolApprovalCheckResult(null, true),
            _ => new ToolApprovalCheckResult(null, false)
        };
    }

    public async Task<ToolApprovalResult> RequestApprovalAsync(
        Guid sessionId,
        Guid invocationId,
        string toolName,
        string? parameters,
        CancellationToken ct = default)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IToolInvocationRepository>();
            var invocation = await repo.GetByIdAsync(invocationId, ct);
            if (invocation == null)
                throw new InvalidOperationException($"Tool invocation {invocationId} not found");

            invocation.ApprovalStatus = "PendingApproval";
            await repo.UpdateAsync(invocation, ct);
        }

        await _streamPublisher.PublishAsync(new SessionStreamEvent
        {
            EventType = "tool.approval_required",
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            Payload = new
            {
                invocationId,
                toolName,
                parameters
            }
        }, ct);

        var tcs = new TaskCompletionSource<ToolApprovalResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingApprovals[invocationId] = tcs;

        try
        {
            using var reg = ct.Register(() => tcs.TrySetCanceled(ct));
            return await tcs.Task;
        }
        finally
        {
            _pendingApprovals.TryRemove(invocationId, out _);
        }
    }

    /// <summary>
    /// Resolve a pending approval. Called by the API when user approves or rejects.
    /// </summary>
    public async Task ResolveApprovalAsync(Guid invocationId, bool approved, string? reason, string? approverId, CancellationToken ct = default)
    {
        if (!_pendingApprovals.TryGetValue(invocationId, out var tcs))
            throw new InvalidOperationException($"No pending approval for invocation {invocationId}");

        using (var scope = _scopeFactory.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IToolInvocationRepository>();
            var invocation = await repo.GetByIdAsync(invocationId, ct);
            if (invocation != null)
            {
                invocation.ApprovalStatus = approved ? "Approved" : "Rejected";
                invocation.ApprovedBy = approverId;
                invocation.ApprovedAt = DateTime.UtcNow;
                await repo.UpdateAsync(invocation, ct);
            }
        }

        tcs.TrySetResult(new ToolApprovalResult(approved, reason));
    }

    /// <summary>
    /// Check if an invocation has a pending approval (for API validation).
    /// </summary>
    public bool HasPendingApproval(Guid invocationId) => _pendingApprovals.ContainsKey(invocationId);
}
