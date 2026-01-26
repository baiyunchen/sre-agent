using Microsoft.Extensions.AI;

namespace SreAgent.Framework.Results;

/// <summary>
/// Agent 执行结果
/// </summary>
public record AgentResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>Agent 输出</summary>
    public string? Output { get; init; }
    
    /// <summary>错误信息</summary>
    public AgentError? Error { get; init; }
    
    /// <summary>执行过程中的所有消息</summary>
    public IReadOnlyList<ChatMessage> Messages { get; init; } = [];
    
    /// <summary>总 Token 使用量</summary>
    public TokenUsage TokenUsage { get; init; } = new();
    
    /// <summary>迭代次数</summary>
    public int IterationCount { get; init; }
    
    /// <summary>是否可重试</summary>
    public bool IsRetryable { get; init; }
    
    public static AgentResult Success(
        string output,
        IReadOnlyList<ChatMessage> messages,
        TokenUsage? tokenUsage = null,
        int iterationCount = 0)
        => new()
        {
            IsSuccess = true,
            Output = output,
            Messages = messages,
            TokenUsage = tokenUsage ?? new TokenUsage(),
            IterationCount = iterationCount
        };
    
    public static AgentResult Failure(
        AgentError error,
        IReadOnlyList<ChatMessage> messages,
        TokenUsage? tokenUsage = null,
        bool isRetryable = true)
        => new()
        {
            IsSuccess = false,
            Error = error,
            Messages = messages,
            TokenUsage = tokenUsage ?? new TokenUsage(),
            IsRetryable = isRetryable
        };
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
