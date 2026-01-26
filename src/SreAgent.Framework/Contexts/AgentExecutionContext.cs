using Microsoft.Extensions.AI;

namespace SreAgent.Framework.Contexts;

/// <summary>
/// Agent 执行时的上下文信息
/// </summary>
public record AgentExecutionContext
{
    /// <summary>会话 ID</summary>
    public Guid SessionId { get; init; } = Guid.NewGuid();
    
    /// <summary>用户输入/任务描述</summary>
    public required string Input { get; init; }
    
    /// <summary>初始消息历史（可选）</summary>
    public IReadOnlyList<ChatMessage>? InitialMessages { get; init; }
    
    /// <summary>执行时传递的变量</summary>
    public Dictionary<string, object> Variables { get; init; } = new();
    
    /// <summary>父 Agent ID（用于多 Agent 场景）</summary>
    public string? ParentAgentId { get; init; }
}
