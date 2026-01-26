using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// Agent 的核心抽象接口
/// </summary>
public interface IAgent
{
    /// <summary>Agent 唯一标识</summary>
    string Id { get; }
    
    /// <summary>Agent 名称</summary>
    string Name { get; }
    
    /// <summary>Agent 描述（用于 System Prompt 或作为 Tool 时的描述）</summary>
    string Description { get; }
    
    /// <summary>执行 Agent</summary>
    Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default);
}
