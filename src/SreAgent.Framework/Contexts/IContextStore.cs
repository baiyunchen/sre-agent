namespace SreAgent.Framework.Contexts;

/// <summary>
/// 上下文存储接口
/// 用于持久化和恢复对话上下文
/// </summary>
public interface IContextStore
{
    /// <summary>保存上下文快照</summary>
    Task SaveAsync(ContextSnapshot snapshot, CancellationToken ct = default);

    /// <summary>获取上下文快照</summary>
    Task<ContextSnapshot?> GetAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>删除上下文快照</summary>
    Task DeleteAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>检查是否存在</summary>
    Task<bool> ExistsAsync(Guid sessionId, CancellationToken ct = default);
}

/// <summary>
/// 内存上下文存储（用于开发和测试）
/// </summary>
public class InMemoryContextStore : IContextStore
{
    private readonly Dictionary<Guid, ContextSnapshot> _store = new();
    private readonly object _lock = new();

    public Task SaveAsync(ContextSnapshot snapshot, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _store[snapshot.SessionId] = snapshot;
        }
        return Task.CompletedTask;
    }

    public Task<ContextSnapshot?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_store.GetValueOrDefault(sessionId));
        }
    }

    public Task DeleteAsync(Guid sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _store.Remove(sessionId);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(Guid sessionId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_store.ContainsKey(sessionId));
        }
    }
}
