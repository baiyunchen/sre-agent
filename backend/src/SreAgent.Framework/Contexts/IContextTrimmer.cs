namespace SreAgent.Framework.Contexts;

/// <summary>
/// 上下文剪枝器接口
/// 每种剪枝策略实现一个独立的类
/// </summary>
public interface IContextTrimmer
{
    /// <summary>策略名称</summary>
    string Name { get; }
    
    /// <summary>
    /// 对上下文进行剪枝
    /// </summary>
    /// <param name="contextManager">上下文管理器</param>
    /// <param name="targetTokens">剪枝后的目标 Token 数</param>
    /// <returns>剪枝结果</returns>
    TrimResult Trim(IContextManager contextManager, int targetTokens);
}

/// <summary>
/// 剪枝结果
/// </summary>
public class TrimResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>剪枝前的 Token 数</summary>
    public int TokensBefore { get; init; }
    
    /// <summary>剪枝后的 Token 数</summary>
    public int TokensAfter { get; init; }
    
    /// <summary>被移除的消息数量</summary>
    public int MessagesRemoved { get; init; }
    
    /// <summary>剪枝策略名称</summary>
    public string StrategyUsed { get; init; } = string.Empty;
    
    /// <summary>错误信息（如果失败）</summary>
    public string? ErrorMessage { get; init; }
    
    public static TrimResult Success(int tokensBefore, int tokensAfter, int messagesRemoved, string strategy)
        => new()
        {
            IsSuccess = true,
            TokensBefore = tokensBefore,
            TokensAfter = tokensAfter,
            MessagesRemoved = messagesRemoved,
            StrategyUsed = strategy
        };
    
    public static TrimResult NoTrimNeeded(int currentTokens, string strategy)
        => new()
        {
            IsSuccess = true,
            TokensBefore = currentTokens,
            TokensAfter = currentTokens,
            MessagesRemoved = 0,
            StrategyUsed = strategy
        };
    
    public static TrimResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage
        };
}
