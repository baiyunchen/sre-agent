namespace SreAgent.Framework.Contexts;

/// <summary>
/// 消息 - 对话中的一条消息
/// </summary>
public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MessageRole Role { get; set; }
    public List<MessagePart> Parts { get; set; } = [];
    public MessageMetadata Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 消息角色
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool
}

/// <summary>
/// 消息元数据
/// </summary>
public class MessageMetadata
{
    /// <summary>来源 Agent ID</summary>
    public string? AgentId { get; set; }
    
    /// <summary>预估 Token 数</summary>
    public int EstimatedTokens { get; set; }
    
    /// <summary>优先级（裁剪时参考）</summary>
    public int Priority { get; set; } = MessagePriority.Normal;
    
    /// <summary>是否可删除</summary>
    public bool IsDeletable { get; set; } = true;
    
    /// <summary>标签（用于分类和过滤）</summary>
    public HashSet<string> Tags { get; set; } = [];
}

/// <summary>
/// 消息优先级常量
/// </summary>
public static class MessagePriority
{
    public const int Critical = 100;    // 绝不删除
    public const int Important = 80;    // 高优先级保留
    public const int Normal = 50;       // 正常优先级
    public const int Low = 20;          // 低优先级
    public const int Disposable = 0;    // 可丢弃
}
