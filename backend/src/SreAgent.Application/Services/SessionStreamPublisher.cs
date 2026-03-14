using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace SreAgent.Application.Services;

/// <summary>
/// In-memory implementation of ISessionStreamPublisher using channels per session.
/// </summary>
public sealed class SessionStreamPublisher : ISessionStreamPublisher
{
    private readonly ConcurrentDictionary<Guid, Channel<SessionStreamEvent>> _channels = new();

    public ValueTask PublishAsync(SessionStreamEvent evt, CancellationToken ct = default)
    {
        if (_channels.TryGetValue(evt.SessionId, out var channel))
        {
            return channel.Writer.WriteAsync(evt, ct);
        }
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<SessionStreamEvent> SubscribeAsync(
        Guid sessionId,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<SessionStreamEvent>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
        _channels[sessionId] = channel;

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct))
            {
                yield return evt;
            }
        }
        finally
        {
            _channels.TryRemove(sessionId, out _);
        }
    }
}
