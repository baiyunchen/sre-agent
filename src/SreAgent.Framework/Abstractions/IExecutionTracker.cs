namespace SreAgent.Framework.Abstractions;

/// <summary>
/// Execution tracking interface - allows persistence of agent runs and tool invocations
/// without coupling the Framework to specific storage implementations.
/// Passed via the variables dictionary with key <see cref="VariableKey"/>.
/// </summary>
public interface IExecutionTracker
{
    public const string VariableKey = "__execution_tracker";

    Task<Guid> OnAgentStartAsync(Guid sessionId, string agentId, string? agentName, CancellationToken ct = default);
    Task OnAgentCompleteAsync(Guid agentRunId, bool isSuccess, string? output, string? errorMessage, long durationMs, CancellationToken ct = default);

    Task<Guid> OnToolStartAsync(Guid agentRunId, string toolName, string? parameters, CancellationToken ct = default);
    Task OnToolCompleteAsync(Guid invocationId, bool isSuccess, string? result, string? errorMessage, long durationMs, CancellationToken ct = default);
}
