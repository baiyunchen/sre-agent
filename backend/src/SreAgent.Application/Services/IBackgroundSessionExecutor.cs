using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Services;

/// <summary>
/// Runs agent execution in background with proper DI scope, registry, and completion handling.
/// </summary>
public interface IBackgroundSessionExecutor
{
    /// <summary>
    /// Starts agent execution in background. Returns immediately.
    /// Caller must have already saved initial context snapshot.
    /// </summary>
    /// <param name="sessionId">Session ID</param>
    /// <param name="context">Prepared context (will be used by agent)</param>
    /// <param name="onComplete">Optional callback to build completion metadata from result</param>
    void StartExecution(
        Guid sessionId,
        IContextManager context,
        Func<AgentResult, Dictionary<string, object>>? onComplete = null);
}
