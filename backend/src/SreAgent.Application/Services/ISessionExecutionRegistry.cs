namespace SreAgent.Application.Services;

/// <summary>
/// Manages per-session CancellationTokenSource for interrupt support.
/// </summary>
public interface ISessionExecutionRegistry
{
    /// <summary>
    /// Register a session with a linked CancellationTokenSource.
    /// The token will be cancelled when Interrupt is called.
    /// </summary>
    void Register(Guid sessionId, CancellationTokenSource cts);

    /// <summary>
    /// Get the CancellationToken for a session, or null if not registered.
    /// </summary>
    CancellationToken? GetToken(Guid sessionId);

    /// <summary>
    /// Cancel the session's execution (triggers Interrupt).
    /// </summary>
    bool Cancel(Guid sessionId);

    /// <summary>
    /// Unregister a session after execution completes.
    /// </summary>
    void Unregister(Guid sessionId);
}
