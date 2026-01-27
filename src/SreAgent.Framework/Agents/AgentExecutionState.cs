using System.Diagnostics;
using Microsoft.Extensions.AI;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Providers;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// Agent 执行状态 - 封装单次执行的所有状态数据
/// </summary>
public sealed class AgentExecutionState
{
    /// <summary>执行唯一标识（用于日志追踪）</summary>
    public required string ExecutionId { get; init; }

    /// <summary>执行计时器</summary>
    public required Stopwatch Stopwatch { get; init; }

    /// <summary>执行上下文</summary>
    public required AgentExecutionContext Context { get; init; }

    /// <summary>消息列表（用于 LLM 调用）</summary>
    public required List<ChatMessage> Messages { get; init; }

    /// <summary>上下文管理器（用于 Token 管理和剪枝）</summary>
    public required DefaultContextManager ContextManager { get; init; }

    /// <summary>Chat 客户端</summary>
    public required IChatClient ChatClient { get; init; }

    /// <summary>模型 Token 限制</summary>
    public required ModelTokenLimits TokenLimits { get; init; }

    /// <summary>累计 Token 使用量</summary>
    public TokenUsage TotalTokenUsage { get; set; } = new();

    /// <summary>当前迭代次数（从 0 开始）</summary>
    public int CurrentIteration { get; set; }

    /// <summary>
    /// 创建新的执行状态
    /// </summary>
    public static AgentExecutionState Create(
        AgentExecutionContext context,
        IChatClient chatClient,
        ModelTokenLimits tokenLimits)
    {
        var tokenEstimator = new SimpleTokenEstimator();
        return new AgentExecutionState
        {
            ExecutionId = Guid.NewGuid().ToString("N")[..8],
            Stopwatch = Stopwatch.StartNew(),
            Context = context,
            Messages = new List<ChatMessage>(),
            ContextManager = new DefaultContextManager(tokenEstimator),
            ChatClient = chatClient,
            TokenLimits = tokenLimits
        };
    }
}
