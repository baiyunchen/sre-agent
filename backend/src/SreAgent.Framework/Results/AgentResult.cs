using SreAgent.Framework.Contexts;

namespace SreAgent.Framework.Results;

/// <summary>
/// Agent 执行结果
/// 包含输出内容和执行后的上下文（可用于继续对话）
/// </summary>
public record AgentResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }

    /// <summary>Agent 输出</summary>
    public string? Output { get; init; }

    /// <summary>错误信息</summary>
    public AgentError? Error { get; init; }

    /// <summary>
    /// 执行后的上下文
    /// 可用于后续追问或持久化
    /// </summary>
    public required IContextManager Context { get; init; }

    /// <summary>总 Token 使用量</summary>
    public TokenUsage TokenUsage { get; init; } = new();

    /// <summary>迭代次数</summary>
    public int IterationCount { get; init; }

    /// <summary>是否可重试</summary>
    public bool IsRetryable { get; init; }

    #region 工厂方法

    public static AgentResult Success(
        string output,
        IContextManager context,
        TokenUsage? tokenUsage = null,
        int iterationCount = 0)
        => new()
        {
            IsSuccess = true,
            Output = output,
            Context = context,
            TokenUsage = tokenUsage ?? new TokenUsage(),
            IterationCount = iterationCount
        };

    public static AgentResult Failure(
        AgentError error,
        IContextManager context,
        TokenUsage? tokenUsage = null,
        bool isRetryable = true)
        => new()
        {
            IsSuccess = false,
            Error = error,
            Context = context,
            TokenUsage = tokenUsage ?? new TokenUsage(),
            IsRetryable = isRetryable
        };

    #endregion
}

/// <summary>
/// Agent 错误信息
/// </summary>
public record AgentError(string Code, string Message, Exception? Exception = null);

/// <summary>
/// Token 使用量统计
/// </summary>
public record TokenUsage(int PromptTokens = 0, int CompletionTokens = 0)
{
    public int TotalTokens => PromptTokens + CompletionTokens;
    
    public static TokenUsage operator +(TokenUsage a, TokenUsage b)
        => new(a.PromptTokens + b.PromptTokens, a.CompletionTokens + b.CompletionTokens);
}
