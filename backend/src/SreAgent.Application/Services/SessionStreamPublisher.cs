using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SreAgent.Application.Services;

/// <summary>
/// In-memory implementation of ISessionStreamPublisher using channels per session.
/// Supports multiple concurrent subscribers per session.
/// </summary>
public sealed class SessionStreamPublisher : ISessionStreamPublisher
{
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Channel<SessionStreamEvent>>> _sessions = new();

    public ValueTask PublishAsync(SessionStreamEvent evt, CancellationToken ct = default)
    {
        if (!_sessions.TryGetValue(evt.SessionId, out var subscribers))
            return ValueTask.CompletedTask;

        foreach (var (_, channel) in subscribers)
        {
            channel.Writer.TryWrite(evt);
        }
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<SessionStreamEvent> SubscribeAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<SessionStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var subscribers = _sessions.GetOrAdd(sessionId, _ => new ConcurrentDictionary<Guid, Channel<SessionStreamEvent>>());
        subscribers[subscriberId] = channel;

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            subscribers.TryRemove(subscriberId, out _);
            if (subscribers.IsEmpty)
                _sessions.TryRemove(sessionId, out _);
        }
    }
}
