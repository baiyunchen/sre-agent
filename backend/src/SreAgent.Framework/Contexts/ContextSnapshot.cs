namespace SreAgent.Framework.Contexts;

/// <summary>
/// 上下文快照 - 持久化友好的数据结构
/// 用于保存和恢复 ContextManager 的状态
/// </summary>
public record ContextSnapshot
{
    /// <summary>会话 ID</summary>
    public Guid SessionId { get; init; }

    /// <summary>快照版本（用于兼容性处理）</summary>
    public int Version { get; init; } = 1;

    /// <summary>System 消息内容</summary>
    public string? SystemMessage { get; init; }

    /// <summary>所有消息（不包含 System 消息）</summary>
    public IReadOnlyList<Message> Messages { get; init; } = [];

    /// <summary>快照创建时间</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>上下文最后更新时间</summary>
    public DateTime? LastUpdatedAt { get; init; }

    /// <summary>当前估算的 Token 数</summary>
    public int EstimatedTokenCount { get; init; }

    /// <summary>消息总数（包含 System 消息）</summary>
    public int TotalMessageCount => Messages.Count + (SystemMessage != null ? 1 : 0);

    /// <summary>
    /// 元数据 - 存储额外的上下文信息
    /// 例如：Agent ID、执行状态、标签等
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// 快照摘要 - 用于快速预览和日志
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// 创建空快照
    /// </summary>
    public static ContextSnapshot Empty(Guid sessionId) => new()
    {
        SessionId = sessionId,
        CreatedAt = DateTime.UtcNow
    };

    /// <summary>
    /// 验证快照是否有效
    /// </summary>
    public bool IsValid()
    {
        // 基本验证
        if (SessionId == Guid.Empty) return false;
        if (Version < 1) return false;

        // 消息验证
        foreach (var message in Messages)
        {
            if (message.Role == MessageRole.System)
            {
                // Messages 列表中不应包含 System 消息
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// 快照元数据键常量
/// </summary>
public static class SnapshotMetadataKeys
{
    /// <summary>创建快照的 Agent ID</summary>
    public const string AgentId = "agent_id";

    /// <summary>快照原因</summary>
    public const string Reason = "reason";

    /// <summary>当前执行迭代次数</summary>
    public const string Iteration = "iteration";

    /// <summary>是否因中断而创建</summary>
    public const string IsInterrupted = "is_interrupted";

    /// <summary>父会话 ID（多 Agent 场景）</summary>
    public const string ParentSessionId = "parent_session_id";

    /// <summary>标签</summary>
    public const string Tags = "tags";
}
