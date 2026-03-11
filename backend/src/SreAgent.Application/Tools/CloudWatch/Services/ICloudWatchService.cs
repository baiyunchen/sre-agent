namespace SreAgent.Application.Tools.CloudWatch.Services;

/// <summary>
/// CloudWatch 日志查询服务接口
/// </summary>
public interface ICloudWatchService
{
    /// <summary>
    /// 简单查询 - 按时间段、Log Group 和关键字进行模糊查询
    /// </summary>
    /// <param name="logGroupName">日志组名称</param>
    /// <param name="startTime">开始时间 (UTC)</param>
    /// <param name="endTime">结束时间 (UTC)</param>
    /// <param name="filterPattern">过滤关键字（支持 CloudWatch 过滤器语法）</param>
    /// <param name="limit">返回结果数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志查询结果</returns>
    Task<CloudWatchQueryResult> SimpleQueryAsync(
        string logGroupName,
        DateTime startTime,
        DateTime endTime,
        string? filterPattern = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CloudWatch Logs Insights 查询 - 使用原始查询语句
    /// </summary>
    /// <param name="logGroupNames">日志组名称列表</param>
    /// <param name="queryString">CloudWatch Logs Insights 查询语句</param>
    /// <param name="startTime">开始时间 (UTC)</param>
    /// <param name="endTime">结束时间 (UTC)</param>
    /// <param name="limit">返回结果数量限制</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志查询结果</returns>
    Task<CloudWatchQueryResult> InsightsQueryAsync(
        IEnumerable<string> logGroupNames,
        string queryString,
        DateTime startTime,
        DateTime endTime,
        int limit = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用的日志组列表
    /// </summary>
    /// <param name="prefix">日志组名称前缀（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>日志组列表</returns>
    Task<IReadOnlyList<LogGroupInfo>> ListLogGroupsAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// CloudWatch 查询结果
/// </summary>
public class CloudWatchQueryResult
{
    /// <summary>是否成功</summary>
    public bool IsSuccess { get; init; }
    
    /// <summary>错误信息（失败时）</summary>
    public string? ErrorMessage { get; init; }
    
    /// <summary>日志事件列表</summary>
    public IReadOnlyList<LogEvent> Events { get; init; } = [];
    
    /// <summary>查询统计信息</summary>
    public QueryStatistics? Statistics { get; init; }
    
    /// <summary>是否有更多结果</summary>
    public bool HasMoreResults { get; init; }
    
    public static CloudWatchQueryResult Success(
        IReadOnlyList<LogEvent> events,
        QueryStatistics? statistics = null,
        bool hasMoreResults = false)
        => new()
        {
            IsSuccess = true,
            Events = events,
            Statistics = statistics,
            HasMoreResults = hasMoreResults
        };
    
    public static CloudWatchQueryResult Failure(string errorMessage)
        => new()
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Events = []
        };
}

/// <summary>
/// 日志事件
/// </summary>
public class LogEvent
{
    /// <summary>事件时间戳</summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>日志消息</summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>日志流名称</summary>
    public string? LogStreamName { get; init; }
    
    /// <summary>事件 ID</summary>
    public string? EventId { get; init; }
    
    /// <summary>额外字段（Insights 查询结果）</summary>
    public Dictionary<string, string>? Fields { get; init; }
}

/// <summary>
/// 查询统计信息
/// </summary>
public class QueryStatistics
{
    /// <summary>扫描的记录数</summary>
    public long RecordsScanned { get; init; }
    
    /// <summary>匹配的记录数</summary>
    public long RecordsMatched { get; init; }
    
    /// <summary>扫描的字节数</summary>
    public long BytesScanned { get; init; }
}

/// <summary>
/// 日志组信息
/// </summary>
public class LogGroupInfo
{
    /// <summary>日志组名称</summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>日志组 ARN</summary>
    public string? Arn { get; init; }
    
    /// <summary>创建时间</summary>
    public DateTime? CreationTime { get; init; }
    
    /// <summary>保留天数</summary>
    public int? RetentionInDays { get; init; }
    
    /// <summary>存储字节数</summary>
    public long? StoredBytes { get; init; }
}
