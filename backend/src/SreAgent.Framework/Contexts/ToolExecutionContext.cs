using System.Text.Json;

namespace SreAgent.Framework.Contexts;

/// <summary>
/// 工具执行上下文
/// 包含工具执行所需的所有信息
/// </summary>
public record ToolExecutionContext
{
    /// <summary>会话 ID</summary>
    public Guid SessionId { get; init; }

    /// <summary>调用方 Agent ID</summary>
    public string AgentId { get; init; } = string.Empty;

    /// <summary>解析后的参数</summary>
    public JsonElement Parameters { get; init; }

    /// <summary>原始参数字符串</summary>
    public string RawArguments { get; init; } = string.Empty;

    /// <summary>从 Agent 传递的变量</summary>
    public IReadOnlyDictionary<string, object> Variables { get; init; }
        = new Dictionary<string, object>();

    /// <summary>工具调用 ID（用于关联请求和响应）</summary>
    public string ToolCallId { get; init; } = string.Empty;

    /// <summary>
    /// 父 Agent 的上下文（用于子 Agent 继承）
    /// 当工具是子 Agent 时，可以从这里获取父上下文摘要
    /// </summary>
    public IContextManager? ParentContext { get; init; }
}
