namespace SreAgent.Framework.Contexts;

/// <summary>
/// 上下文管理器接口
/// Framework 提供默认实现，业务层可扩展
/// </summary>
public interface IContextManager
{
    /// <summary>添加消息到上下文</summary>
    void AddMessage(Message message);
    
    /// <summary>批量添加消息</summary>
    void AddMessages(IEnumerable<Message> messages);
    
    /// <summary>获取所有消息</summary>
    IReadOnlyList<Message> GetMessages();
    
    /// <summary>当前 Token 使用量估算</summary>
    int EstimatedTokenCount { get; }
    
    /// <summary>清空上下文（保留 System 消息）</summary>
    void Clear();
    
    /// <summary>设置 System 消息（会替换已有的）</summary>
    void SetSystemMessage(string content);
    
    /// <summary>获取 System 消息</summary>
    Message? GetSystemMessage();
}

/// <summary>
/// 上下文管理器配置选项
/// </summary>
public class ContextManagerOptions
{
    /// <summary>是否自动压缩长工具结果</summary>
    public bool AutoCompressToolResults { get; set; } = true;
    
    /// <summary>工具结果压缩阈值（Token 数）</summary>
    public int ToolResultCompressThreshold { get; set; } = 1000;
}
