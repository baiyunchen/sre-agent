namespace SreAgent.Framework.Abstractions;

/// <summary>
/// Result of checking an approval rule.
/// </summary>
public readonly record struct ToolApprovalCheckResult(bool? AllowDirect, bool RequiresApproval);

/// <summary>
/// Checks approval rules and requests human approval for tool invocations.
/// Passed via variables with key <see cref="VariableKey"/>.
/// </summary>
public interface IToolApprovalChecker
{
    public const string VariableKey = "__tool_approval_checker";

    /// <summary>
    /// Check the approval rule for a tool.
    /// </summary>
    /// <returns>
    /// AllowDirect=true, RequiresApproval=false: execute directly (always-allow)
    /// AllowDirect=false, RequiresApproval=false: reject (always-deny)
    /// RequiresApproval=true: call RequestApprovalAsync
    /// AllowDirect=null, RequiresApproval=false: no rule, default allow
    /// </returns>
    Task<ToolApprovalCheckResult> CheckRuleAsync(string toolName, CancellationToken ct = default);

    /// <summary>
    /// Request approval for a tool invocation. Blocks until approved/rejected or timeout.
    /// </summary>
    Task<ToolApprovalResult> RequestApprovalAsync(
        Guid sessionId,
        Guid invocationId,
        string toolName,
        string? parameters,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a tool approval decision.
/// </summary>
public record ToolApprovalResult(bool IsApproved, string? Reason = null);
