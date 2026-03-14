using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

/// <summary>
/// Decorator that publishes execution events to ISessionStreamPublisher.
/// </summary>
public sealed class StreamingExecutionTracker : IExecutionTracker
{
    private readonly PersistenceExecutionTracker _inner;
    private readonly ISessionStreamPublisher _publisher;
    private readonly IAgentRunRepository _agentRunRepository;
    private readonly IToolInvocationRepository _toolInvocationRepository;

    public StreamingExecutionTracker(
        PersistenceExecutionTracker inner,
        ISessionStreamPublisher publisher,
        IAgentRunRepository agentRunRepository,
        IToolInvocationRepository toolInvocationRepository)
    {
        _inner = inner;
        _publisher = publisher;
        _agentRunRepository = agentRunRepository;
        _toolInvocationRepository = toolInvocationRepository;
    }

    public async Task<Guid> OnAgentStartAsync(Guid sessionId, string agentId, string? agentName, CancellationToken ct = default)
    {
        var agentRunId = await _inner.OnAgentStartAsync(sessionId, agentId, agentName, ct);
        await _publisher.PublishAsync(new SessionStreamEvent
        {
            EventType = "agent.started",
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow,
            Payload = new { agentRunId, agentId, agentName }
        }, ct);
        return agentRunId;
    }

    public async Task OnAgentCompleteAsync(Guid agentRunId, bool isSuccess, string? output, string? errorMessage, long durationMs, CancellationToken ct = default)
    {
        await _inner.OnAgentCompleteAsync(agentRunId, isSuccess, output, errorMessage, durationMs, ct);
        var run = await _agentRunRepository.GetByIdAsync(agentRunId, ct);
        if (run != null)
        {
            await _publisher.PublishAsync(new SessionStreamEvent
            {
                EventType = "agent.completed",
                SessionId = run.SessionId,
                Timestamp = DateTime.UtcNow,
                Payload = new { agentRunId, isSuccess, durationMs }
            }, ct);
        }
    }

    public async Task<Guid> OnToolStartAsync(Guid agentRunId, string toolName, string? parameters, CancellationToken ct = default)
    {
        var invocationId = await _inner.OnToolStartAsync(agentRunId, toolName, parameters, ct);
        var run = await _agentRunRepository.GetByIdAsync(agentRunId, ct);
        if (run != null)
        {
            await _publisher.PublishAsync(new SessionStreamEvent
            {
                EventType = "tool.started",
                SessionId = run.SessionId,
                Timestamp = DateTime.UtcNow,
                Payload = new { agentRunId, invocationId, toolName, parameters }
            }, ct);
        }
        return invocationId;
    }

    public async Task OnToolCompleteAsync(Guid invocationId, bool isSuccess, string? result, string? errorMessage, long durationMs, CancellationToken ct = default)
    {
        await _inner.OnToolCompleteAsync(invocationId, isSuccess, result, errorMessage, durationMs, ct);
        var invocation = await _toolInvocationRepository.GetByIdAsync(invocationId, ct);
        if (invocation != null)
        {
            var run = await _agentRunRepository.GetByIdAsync(invocation.AgentRunId, ct);
            if (run != null)
            {
                await _publisher.PublishAsync(new SessionStreamEvent
                {
                    EventType = "tool.completed",
                    SessionId = run.SessionId,
                    Timestamp = DateTime.UtcNow,
                    Payload = new { invocationId, toolName = invocation.ToolName, isSuccess, durationMs }
                }, ct);
            }
        }
    }
}
