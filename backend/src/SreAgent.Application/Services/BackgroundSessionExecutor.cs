using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Services;

/// <summary>
/// Runs agent execution in background with proper DI scope, registry, and completion handling.
/// </summary>
public sealed class BackgroundSessionExecutor : IBackgroundSessionExecutor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISessionExecutionRegistry _registry;
    private readonly ISessionStreamPublisher _streamPublisher;
    private readonly ILogger<BackgroundSessionExecutor> _logger;

    public BackgroundSessionExecutor(
        IServiceScopeFactory scopeFactory,
        ISessionExecutionRegistry registry,
        ISessionStreamPublisher streamPublisher,
        ILogger<BackgroundSessionExecutor> logger)
    {
        _scopeFactory = scopeFactory;
        _registry = registry;
        _streamPublisher = streamPublisher;
        _logger = logger;
    }

    public void StartExecution(
        Guid sessionId,
        IContextManager context,
        Func<AgentResult, Dictionary<string, object>>? onComplete = null)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var agent = scope.ServiceProvider.GetRequiredService<IAgent>();
            var tracker = scope.ServiceProvider.GetRequiredService<IExecutionTracker>();
            var contextStore = scope.ServiceProvider.GetRequiredService<IContextStore>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

            var cts = new CancellationTokenSource();
            _registry.Register(sessionId, cts);

            try
            {
                var variables = new Dictionary<string, object>
                {
                    [IExecutionTracker.VariableKey] = tracker
                };

                var stopwatch = Stopwatch.StartNew();
                var result = await agent.ExecuteAsync(context, variables, cts.Token);
                stopwatch.Stop();

                var metadata = onComplete != null
                    ? onComplete(result)
                    : BuildDefaultMetadata(result, stopwatch.ElapsedMilliseconds);

                await contextStore.SaveAsync(
                    result.Context.ExportSnapshot(sessionId, metadata), CancellationToken.None);

                await auditService.LogAsync(
                    sessionId,
                    result.IsSuccess ? "SessionCompleted" : "SessionFailed",
                    result.IsSuccess
                        ? $"Background execution completed in {stopwatch.ElapsedMilliseconds}ms"
                        : $"Background execution failed: {result.Error?.Message}",
                    new { durationMs = stopwatch.ElapsedMilliseconds, result.IterationCount },
                    "system", null, CancellationToken.None);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Session {SessionId} execution was cancelled", sessionId);
                await auditService.LogAsync(
                    sessionId, "SessionCancelled",
                    "Background execution was cancelled",
                    null, "system", null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background execution failed for session {SessionId}", sessionId);
                try
                {
                    var metadata = new Dictionary<string, object>
                    {
                        ["status"] = "Failed",
                        ["completed_at"] = DateTime.UtcNow,
                        ["diagnosis_summary"] = ex.Message
                    };
                    await contextStore.SaveAsync(
                        context.ExportSnapshot(sessionId, metadata), CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    _logger.LogWarning(saveEx, "Failed to save failed state for session {SessionId}", sessionId);
                }

                await auditService.LogAsync(
                    sessionId, "SessionFailed",
                    $"Background execution failed: {ex.Message}",
                    new { error = ex.Message }, "system", null, CancellationToken.None);
            }
            finally
            {
                _registry.Unregister(sessionId);
                await _streamPublisher.PublishAsync(new SessionStreamEvent
                {
                    EventType = "session.ended",
                    SessionId = sessionId,
                    Timestamp = DateTime.UtcNow,
                    Payload = null
                }, CancellationToken.None);
            }
        });
    }

    private static Dictionary<string, object> BuildDefaultMetadata(AgentResult result, long durationMs)
    {
        var sessionStatus = result.IsSuccess ? "Completed" : "Failed";
        return new Dictionary<string, object>
        {
            ["status"] = sessionStatus,
            ["completed_at"] = DateTime.UtcNow,
            ["current_step"] = result.IterationCount,
            ["diagnosis_summary"] = result.Output ?? string.Empty,
            ["prompt_tokens"] = result.TokenUsage.PromptTokens,
            ["completion_tokens"] = result.TokenUsage.CompletionTokens,
            ["total_tokens"] = result.TokenUsage.TotalTokens
        };
    }
}
