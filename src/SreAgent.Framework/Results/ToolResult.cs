namespace SreAgent.Framework.Results;

/// <summary>
/// 工具执行结果（Result 模式）
/// 无论成功失败，都返回结果而非抛异常
/// </summary>
public record ToolResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>
    /// 结果内容
    /// - 成功时：返回的数据/信息
    /// - 失败时：错误描述（供 LLM 理解）
    /// </summary>
    public string Content { get; init; } = string.Empty;
    
    /// <summary>结构化数据（可选，用于程序处理）</summary>
    public object? Data { get; init; }
    
    /// <summary>错误代码（失败时）</summary>
    public string? ErrorCode { get; init; }
    
    /// <summary>
    /// 是否可重试
    /// - true: LLM 可尝试修正参数后重试
    /// - false: 永久性错误，无需重试
    /// </summary>
    public bool IsRetryable { get; init; }
    
    /// <summary>执行耗时</summary>
    public TimeSpan Duration { get; init; }
    
    #region 工厂方法
    
    /// <summary>创建成功结果</summary>
    public static ToolResult Success(string content, object? data = null)
        => new()
        {
            IsSuccess = true,
            Content = content,
            Data = data
        };
    
    /// <summary>创建失败结果</summary>
    public static ToolResult Failure(
        string errorMessage,
        string? errorCode = null,
        bool isRetryable = true)
        => new()
        {
            IsSuccess = false,
            Content = errorMessage,
            ErrorCode = errorCode,
            IsRetryable = isRetryable
        };
    
    /// <summary>从异常创建失败结果</summary>
    public static ToolResult FromException(Exception ex, bool? isRetryable = null)
    {
        var retryable = isRetryable ?? IsExceptionRetryable(ex);
        return new ToolResult
        {
            IsSuccess = false,
            Content = $"Tool execution failed: {ex.Message}",
            ErrorCode = ex.GetType().Name,
            IsRetryable = retryable
        };
    }
    
    private static bool IsExceptionRetryable(Exception ex) => ex switch
    {
        TimeoutException => true,
        HttpRequestException => true,
        OperationCanceledException => false,
        ArgumentException => false,
        _ => true
    };
    
    #endregion
}
