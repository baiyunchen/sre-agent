using SreAgent.Framework.Contexts;
using SreAgent.Framework.Options;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Abstractions;

/// <summary>
/// Agent 的核心抽象接口
/// Agent 是无状态的执行引擎，所有对话状态都在 IContextManager 中管理
/// </summary>
public interface IAgent
{
    /// <summary>Agent 唯一标识</summary>
    string Id { get; }

    /// <summary>Agent 名称</summary>
    string Name { get; }

    /// <summary>Agent 描述（用于 System Prompt 或作为 Tool 时的描述）</summary>
    string Description { get; }

    /// <summary>Agent 配置选项</summary>
    AgentOptions Options { get; }

    /// <summary>
    /// 执行 Agent
    /// </summary>
    /// <param name="context">
    /// 对话上下文，应该已经包含：
    /// - System Prompt（如果需要）
    /// - 用户输入（最后一条消息）
    /// - 历史消息（如果是追问）
    /// </param>
    /// <param name="variables">传递给工具的运行时变量（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>执行结果</returns>
    Task<AgentResult> ExecuteAsync(
        IContextManager context,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);
}
