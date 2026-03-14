using System.Collections.Concurrent;

namespace SreAgent.Application.Services;

/// <summary>
/// In-memory implementation of ISessionExecutionRegistry.
/// </summary>
public sealed class SessionExecutionRegistry : ISessionExecutionRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sources = new();

    public void Register(Guid sessionId, CancellationTokenSource cts)
    {
        _sources.AddOrUpdate(sessionId, cts, (_, oldCts) =>
        {
            try { oldCts.Cancel(); oldCts.Dispose(); }
            catch { /* best effort */ }
            return cts;
        });
    }

    public CancellationToken? GetToken(Guid sessionId)
    {
        return _sources.TryGetValue(sessionId, out var cts) ? cts.Token : null;
    }

    public bool Cancel(Guid sessionId)
    {
        if (_sources.TryGetValue(sessionId, out var cts))
        {
            try
            {
                cts.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
        return false;
    }

    public void Unregister(Guid sessionId)
    {
        if (_sources.TryRemove(sessionId, out var cts))
        {
            try
            {
                cts.Dispose();
            }
            catch
            {
                // best effort
            }
        }
    }
}
