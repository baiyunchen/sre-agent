namespace SreAgent.Application.Services;

/// <summary>
/// Publishes per-session execution events for SSE streaming.
/// </summary>
public interface ISessionStreamPublisher
{
    /// <summary>
    /// Publish an event to all subscribers of the session.
    /// </summary>
    ValueTask PublishAsync(SessionStreamEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to events for a session. Returns an async enumerable that completes when the session ends or the cancellation is requested.
    /// </summary>
    IAsyncEnumerable<SessionStreamEvent> SubscribeAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// Event payload for per-session SSE stream.
/// </summary>
public sealed class SessionStreamEvent
{
    public required string EventType { get; init; }
    public required Guid SessionId { get; init; }
    public required DateTime Timestamp { get; init; }
    public object? Payload { get; init; }
}
