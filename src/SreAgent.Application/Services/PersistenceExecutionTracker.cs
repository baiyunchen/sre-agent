using System.Text.Json;
using SreAgent.Framework.Abstractions;
using SreAgent.Repository.Entities;
using SreAgent.Repository.Repositories;

namespace SreAgent.Application.Services;

public class PersistenceExecutionTracker : IExecutionTracker
{
    private readonly IAgentRunRepository _agentRunRepository;
    private readonly IToolInvocationRepository _toolInvocationRepository;

    public PersistenceExecutionTracker(
        IAgentRunRepository agentRunRepository,
        IToolInvocationRepository toolInvocationRepository)
    {
        _agentRunRepository = agentRunRepository;
        _toolInvocationRepository = toolInvocationRepository;
    }

    public async Task<Guid> OnAgentStartAsync(Guid sessionId, string agentId, string? agentName, CancellationToken ct = default)
    {
        var run = new AgentRunEntity
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            AgentId = agentId,
            AgentName = agentName,
            Status = "Running",
            StartedAt = DateTime.UtcNow
        };
        await _agentRunRepository.CreateAsync(run, ct);
        return run.Id;
    }

    public async Task OnAgentCompleteAsync(Guid agentRunId, bool isSuccess, string? output, string? errorMessage, long durationMs, CancellationToken ct = default)
    {
        var run = await _agentRunRepository.GetByIdAsync(agentRunId, ct);
        if (run == null) return;

        run.Status = isSuccess ? "Completed" : "Failed";
        run.Output = !string.IsNullOrEmpty(output)
            ? JsonSerializer.SerializeToDocument(new { text = Truncate(output, 10000) })
            : null;
        run.ErrorMessage = errorMessage;
        run.CompletedAt = DateTime.UtcNow;
        run.DurationMs = durationMs;

        await _agentRunRepository.UpdateAsync(run, ct);
    }

    public async Task<Guid> OnToolStartAsync(Guid agentRunId, string toolName, string? parameters, CancellationToken ct = default)
    {
        JsonDocument? paramDoc = null;
        if (!string.IsNullOrEmpty(parameters))
        {
            try { paramDoc = JsonDocument.Parse(parameters); }
            catch { paramDoc = JsonSerializer.SerializeToDocument(new { raw = Truncate(parameters, 5000) }); }
        }

        var invocation = new ToolInvocationEntity
        {
            Id = Guid.NewGuid(),
            AgentRunId = agentRunId,
            ToolName = toolName,
            Parameters = paramDoc,
            Status = "Running",
            RequestedAt = DateTime.UtcNow,
            ExecutedAt = DateTime.UtcNow
        };
        await _toolInvocationRepository.CreateAsync(invocation, ct);
        return invocation.Id;
    }

    public async Task OnToolCompleteAsync(Guid invocationId, bool isSuccess, string? result, string? errorMessage, long durationMs, CancellationToken ct = default)
    {
        var invocation = await _toolInvocationRepository.GetByIdAsync(invocationId, ct);
        if (invocation == null) return;

        invocation.Status = isSuccess ? "Completed" : "Failed";
        invocation.Result = !string.IsNullOrEmpty(result)
            ? JsonSerializer.SerializeToDocument(new { text = Truncate(result, 10000) })
            : null;
        invocation.ErrorMessage = errorMessage;
        invocation.CompletedAt = DateTime.UtcNow;
        invocation.DurationMs = durationMs;

        await _toolInvocationRepository.UpdateAsync(invocation, ct);
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
