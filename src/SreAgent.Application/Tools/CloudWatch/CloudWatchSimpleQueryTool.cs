using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.CloudWatch.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Prompts;
using SreAgent.Framework.Results;

namespace SreAgent.Application.Tools.CloudWatch;

/// <summary>
/// CloudWatch 简单日志查询工具
/// 按时间段、日志组和关键字进行模糊查询
/// </summary>
public class CloudWatchSimpleQueryTool : ToolBase<CloudWatchSimpleQueryParams>
{
    private static readonly Lazy<string> PromptContent = PromptLoader.CreateLazy<CloudWatchSimpleQueryTool>(
        "CloudWatchSimpleQueryPrompt.txt",
        "Search CloudWatch logs by time range, log group, and keyword filter.");

    private readonly ICloudWatchService _cloudWatchService;
    private readonly IDiagnosticDataService? _diagnosticDataService;
    private const int DiagnosticStoreThreshold = 20;

    public CloudWatchSimpleQueryTool(ICloudWatchService cloudWatchService, IDiagnosticDataService? diagnosticDataService = null)
    {
        _cloudWatchService = cloudWatchService;
        _diagnosticDataService = diagnosticDataService;
    }

    public override string Name => "cloudwatch_simple_query";

    public override string Summary => "Search CloudWatch logs by time range and keyword";

    public override string Description => PromptContent.Value;

    public override string Category => "AWS Monitoring";

    protected override string ReturnDescription =>
        "Returns matching log events with timestamps and messages";

    protected override async Task<ToolResult> ExecuteAsync(
        CloudWatchSimpleQueryParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 解析时间参数
        var (startTime, endTime) = ParseTimeRange(parameters);

        // 执行查询
        var result = await _cloudWatchService.SimpleQueryAsync(
            parameters.LogGroupName,
            startTime,
            endTime,
            parameters.FilterPattern,
            parameters.Limit ?? 100,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToolResult.Failure(
                result.ErrorMessage ?? "查询失败",
                "CLOUDWATCH_QUERY_FAILED",
                isRetryable: true);
        }

        if (result.Events.Count == 0)
        {
            return ToolResult.Success(
                FormatNoResults(parameters, startTime, endTime),
                new { events = Array.Empty<object>(), count = 0 });
        }

        if (_diagnosticDataService != null && result.Events.Count > DiagnosticStoreThreshold)
        {
            var stored = await StoreDiagnosticDataAsync(
                context.SessionId, parameters.LogGroupName, result.Events, cancellationToken);
            return ToolResult.Success(
                FormatSummaryWithStorageNote(result, parameters, startTime, endTime, stored),
                new { count = result.Events.Count, storedToDiagnostics = stored, hasMore = result.HasMoreResults });
        }

        return ToolResult.Success(
            FormatResults(result, parameters, startTime, endTime),
            new
            {
                events = result.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    logStream = e.LogStreamName
                }),
                count = result.Events.Count,
                hasMore = result.HasMoreResults
            });
    }

    private async Task<int> StoreDiagnosticDataAsync(
        Guid sessionId, string logGroupName, IReadOnlyList<LogEvent> events, CancellationToken ct)
    {
        var records = events.Select(e => new DiagnosticDataInput
        {
            SourceType = "cloudwatch_logs",
            SourceName = logGroupName,
            LogTimestamp = e.Timestamp,
            Content = e.Message,
            Severity = InferSeverity(e.Message)
        });
        return await _diagnosticDataService!.StoreBatchAsync(sessionId, records, ct);
    }

    private static string FormatSummaryWithStorageNote(
        CloudWatchQueryResult result,
        CloudWatchSimpleQueryParams parameters,
        DateTime startTime, DateTime endTime, int storedCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 CloudWatch 日志查询结果");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine($"日志组: {parameters.LogGroupName}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        if (!string.IsNullOrWhiteSpace(parameters.FilterPattern))
            sb.AppendLine($"过滤条件: {parameters.FilterPattern}");
        sb.AppendLine($"总记录数: {result.Events.Count}");
        sb.AppendLine($"💾 已将 {storedCount} 条记录存入诊断数据库，可使用 search_diagnostic_data 或 query_diagnostic_data 工具检索。");
        sb.AppendLine();
        sb.AppendLine("前 5 条记录预览:");
        foreach (var evt in result.Events.Take(5))
            sb.AppendLine($"[{evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {evt.Message}");
        if (result.Events.Count > 5)
            sb.AppendLine($"... 还有 {result.Events.Count - 5} 条记录，请使用诊断数据查询工具获取。");
        return sb.ToString();
    }

    private static string? InferSeverity(string message)
    {
        var upper = message.ToUpperInvariant();
        if (upper.Contains("ERROR") || upper.Contains("FATAL") || upper.Contains("CRITICAL"))
            return "ERROR";
        if (upper.Contains("WARN"))
            return "WARNING";
        if (upper.Contains("DEBUG") || upper.Contains("TRACE"))
            return "DEBUG";
        return "INFO";
    }

    private static (DateTime startTime, DateTime endTime) ParseTimeRange(CloudWatchSimpleQueryParams parameters)
    {
        DateTime endTime;
        DateTime startTime;

        // 解析结束时间
        if (!string.IsNullOrWhiteSpace(parameters.EndTime))
        {
            if (!DateTime.TryParse(parameters.EndTime, out endTime))
            {
                endTime = DateTime.UtcNow;
            }
            else if (endTime.Kind != DateTimeKind.Utc)
            {
                endTime = endTime.ToUniversalTime();
            }
        }
        else
        {
            endTime = DateTime.UtcNow;
        }

        // 解析开始时间
        if (!string.IsNullOrWhiteSpace(parameters.StartTime))
        {
            if (!DateTime.TryParse(parameters.StartTime, out startTime))
            {
                // 解析相对时间（如 "1h", "30m", "7d"）
                startTime = ParseRelativeTime(parameters.StartTime, endTime);
            }
            else if (startTime.Kind != DateTimeKind.Utc)
            {
                startTime = startTime.ToUniversalTime();
            }
        }
        else
        {
            // 默认查询最近 1 小时
            startTime = endTime.AddHours(-1);
        }

        return (startTime, endTime);
    }

    private static DateTime ParseRelativeTime(string relativeTime, DateTime baseTime)
    {
        relativeTime = relativeTime.Trim().ToLowerInvariant();

        // 移除开头的负号（如果有）
        if (relativeTime.StartsWith('-'))
        {
            relativeTime = relativeTime[1..];
        }

        // 解析数字和单位
        var numericPart = new string(relativeTime.TakeWhile(char.IsDigit).ToArray());
        var unitPart = relativeTime[numericPart.Length..].Trim();

        if (!int.TryParse(numericPart, out var value))
        {
            value = 1;
        }

        return unitPart switch
        {
            "m" or "min" or "mins" or "minute" or "minutes" => baseTime.AddMinutes(-value),
            "h" or "hr" or "hrs" or "hour" or "hours" => baseTime.AddHours(-value),
            "d" or "day" or "days" => baseTime.AddDays(-value),
            "w" or "week" or "weeks" => baseTime.AddDays(-value * 7),
            _ => baseTime.AddHours(-1) // 默认 1 小时
        };
    }

    private static string FormatNoResults(
        CloudWatchSimpleQueryParams parameters,
        DateTime startTime,
        DateTime endTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 CloudWatch 日志查询结果");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine($"日志组: {parameters.LogGroupName}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        if (!string.IsNullOrWhiteSpace(parameters.FilterPattern))
        {
            sb.AppendLine($"过滤条件: {parameters.FilterPattern}");
        }
        sb.AppendLine();
        sb.AppendLine("⚠️ 未找到匹配的日志记录");
        return sb.ToString();
    }

    private static string FormatResults(
        CloudWatchQueryResult result,
        CloudWatchSimpleQueryParams parameters,
        DateTime startTime,
        DateTime endTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 CloudWatch 日志查询结果");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine($"日志组: {parameters.LogGroupName}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        if (!string.IsNullOrWhiteSpace(parameters.FilterPattern))
        {
            sb.AppendLine($"过滤条件: {parameters.FilterPattern}");
        }
        sb.AppendLine($"返回记录数: {result.Events.Count}{(result.HasMoreResults ? " (还有更多)" : "")}");
        sb.AppendLine("─".PadRight(50, '─'));
        sb.AppendLine();

        foreach (var evt in result.Events)
        {
            sb.AppendLine($"[{evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {evt.Message}");
        }

        return sb.ToString();
    }
}

#region Parameters

/// <summary>
/// CloudWatch 简单查询参数
/// </summary>
public class CloudWatchSimpleQueryParams
{
    /// <summary>
    /// 日志组名称
    /// </summary>
    [Required]
    [Description("The name of the CloudWatch log group to search (e.g., '/aws/lambda/my-function', '/ecs/my-service')")]
    public string LogGroupName { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间（支持 ISO 8601 格式或相对时间如 "1h", "30m", "7d"）
    /// </summary>
    [Description("Start time for the query. Supports ISO 8601 format (e.g., '2024-01-15T10:00:00Z') or relative time (e.g., '1h' for 1 hour ago, '30m' for 30 minutes ago, '7d' for 7 days ago). Defaults to 1 hour ago if not specified.")]
    public string? StartTime { get; set; }

    /// <summary>
    /// 结束时间（支持 ISO 8601 格式，默认为当前时间）
    /// </summary>
    [Description("End time for the query. Supports ISO 8601 format. Defaults to current time if not specified.")]
    public string? EndTime { get; set; }

    /// <summary>
    /// 过滤关键字（支持 CloudWatch 过滤器语法）
    /// </summary>
    [Description("Filter pattern to search for in log messages. Supports CloudWatch filter pattern syntax. Examples: 'ERROR', '\"Connection refused\"', '?ERROR ?WARN' (OR), 'ERROR -DEBUG' (exclude DEBUG).")]
    public string? FilterPattern { get; set; }

    /// <summary>
    /// 返回结果数量限制
    /// </summary>
    [Description("Maximum number of log events to return. Default is 100, maximum is 10000.")]
    [Range(1, 10000)]
    public int? Limit { get; set; }
}

#endregion
