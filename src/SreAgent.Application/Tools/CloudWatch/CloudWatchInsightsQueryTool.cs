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
/// CloudWatch Logs Insights 查询工具
/// 使用原始 CloudWatch Logs Insights 查询语句进行高级查询
/// </summary>
public class CloudWatchInsightsQueryTool : ToolBase<CloudWatchInsightsQueryParams>
{
    private static readonly Lazy<string> PromptContent = PromptLoader.CreateLazy<CloudWatchInsightsQueryTool>(
        "CloudWatchInsightsQueryPrompt.txt",
        "Execute CloudWatch Logs Insights queries with full query language support.");

    private readonly ICloudWatchService _cloudWatchService;
    private readonly IDiagnosticDataService? _diagnosticDataService;
    private const int DiagnosticStoreThreshold = 20;

    public CloudWatchInsightsQueryTool(ICloudWatchService cloudWatchService, IDiagnosticDataService? diagnosticDataService = null)
    {
        _cloudWatchService = cloudWatchService;
        _diagnosticDataService = diagnosticDataService;
    }

    public override string Name => "cloudwatch_insights_query";

    public override string Summary => "Execute CloudWatch Logs Insights query with full query language";

    public override string Description => PromptContent.Value;

    public override string Category => "AWS Monitoring";

    protected override string ReturnDescription =>
        "Returns query results with parsed fields, statistics, and formatted output";

    protected override async Task<ToolResult> ExecuteAsync(
        CloudWatchInsightsQueryParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 验证日志组列表
        var logGroups = ParseLogGroups(parameters.LogGroupNames);
        if (logGroups.Count == 0)
        {
            return ToolResult.Failure(
                "至少需要指定一个日志组",
                "INVALID_PARAMETERS",
                isRetryable: true);
        }

        // 解析时间参数
        var (startTime, endTime) = ParseTimeRange(parameters);

        // 执行查询
        var result = await _cloudWatchService.InsightsQueryAsync(
            logGroups,
            parameters.QueryString,
            startTime,
            endTime,
            parameters.Limit ?? 1000,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return ToolResult.Failure(
                result.ErrorMessage ?? "查询失败",
                "CLOUDWATCH_INSIGHTS_QUERY_FAILED",
                isRetryable: true);
        }

        if (result.Events.Count == 0)
        {
            return ToolResult.Success(
                FormatNoResults(parameters, logGroups, startTime, endTime),
                new
                {
                    events = Array.Empty<object>(),
                    count = 0,
                    statistics = result.Statistics
                });
        }

        if (_diagnosticDataService != null && result.Events.Count > DiagnosticStoreThreshold)
        {
            var stored = await StoreDiagnosticDataAsync(
                context.SessionId, string.Join(",", logGroups), result.Events, cancellationToken);
            return ToolResult.Success(
                FormatSummaryWithStorageNote(result, parameters, logGroups, startTime, endTime, stored),
                new { count = result.Events.Count, storedToDiagnostics = stored, statistics = result.Statistics });
        }

        return ToolResult.Success(
            FormatResults(result, parameters, logGroups, startTime, endTime),
            new
            {
                events = result.Events.Select(e => new
                {
                    timestamp = e.Timestamp,
                    message = e.Message,
                    logStream = e.LogStreamName,
                    fields = e.Fields
                }),
                count = result.Events.Count,
                statistics = result.Statistics != null
                    ? new
                    {
                        recordsScanned = result.Statistics.RecordsScanned,
                        recordsMatched = result.Statistics.RecordsMatched,
                        bytesScanned = result.Statistics.BytesScanned
                    }
                    : null
            });
    }

    private async Task<int> StoreDiagnosticDataAsync(
        Guid sessionId, string logGroupName, IReadOnlyList<LogEvent> events, CancellationToken ct)
    {
        var records = events.Select(e => new DiagnosticDataInput
        {
            SourceType = "cloudwatch_insights",
            SourceName = logGroupName,
            LogTimestamp = e.Timestamp,
            Content = e.Message,
            StructuredFields = e.Fields?.ToDictionary(kv => kv.Key, kv => (object)kv.Value),
            Severity = InferSeverity(e.Message)
        });
        return await _diagnosticDataService!.StoreBatchAsync(sessionId, records, ct);
    }

    private static string FormatSummaryWithStorageNote(
        CloudWatchQueryResult result,
        CloudWatchInsightsQueryParams parameters,
        List<string> logGroups,
        DateTime startTime, DateTime endTime, int storedCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📊 CloudWatch Logs Insights 查询结果");
        sb.AppendLine("─".PadRight(60, '─'));
        sb.AppendLine($"日志组: {string.Join(", ", logGroups)}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"查询语句: {parameters.QueryString}");
        if (result.Statistics != null)
        {
            sb.AppendLine($"扫描记录数: {result.Statistics.RecordsScanned:N0}");
            sb.AppendLine($"匹配记录数: {result.Statistics.RecordsMatched:N0}");
        }
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

    private static List<string> ParseLogGroups(string logGroupNames)
    {
        if (string.IsNullOrWhiteSpace(logGroupNames))
        {
            return [];
        }

        // 支持逗号分隔或换行分隔
        return logGroupNames
            .Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .ToList();
    }

    private static (DateTime startTime, DateTime endTime) ParseTimeRange(CloudWatchInsightsQueryParams parameters)
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

        if (relativeTime.StartsWith('-'))
        {
            relativeTime = relativeTime[1..];
        }

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
            _ => baseTime.AddHours(-1)
        };
    }

    private static string FormatNoResults(
        CloudWatchInsightsQueryParams parameters,
        List<string> logGroups,
        DateTime startTime,
        DateTime endTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📊 CloudWatch Logs Insights 查询结果");
        sb.AppendLine("─".PadRight(60, '─'));
        sb.AppendLine($"日志组: {string.Join(", ", logGroups)}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"查询语句:");
        sb.AppendLine($"  {parameters.QueryString}");
        sb.AppendLine();
        sb.AppendLine("⚠️ 查询未返回任何结果");
        return sb.ToString();
    }

    private static string FormatResults(
        CloudWatchQueryResult result,
        CloudWatchInsightsQueryParams parameters,
        List<string> logGroups,
        DateTime startTime,
        DateTime endTime)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📊 CloudWatch Logs Insights 查询结果");
        sb.AppendLine("─".PadRight(60, '─'));
        sb.AppendLine($"日志组: {string.Join(", ", logGroups)}");
        sb.AppendLine($"时间范围: {startTime:yyyy-MM-dd HH:mm:ss} UTC - {endTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"查询语句:");
        sb.AppendLine($"  {parameters.QueryString}");

        if (result.Statistics != null)
        {
            sb.AppendLine();
            sb.AppendLine("📈 查询统计:");
            sb.AppendLine($"  扫描记录数: {result.Statistics.RecordsScanned:N0}");
            sb.AppendLine($"  匹配记录数: {result.Statistics.RecordsMatched:N0}");
            sb.AppendLine($"  扫描数据量: {FormatBytes(result.Statistics.BytesScanned)}");
        }

        sb.AppendLine();
        sb.AppendLine($"返回记录数: {result.Events.Count}");
        sb.AppendLine("─".PadRight(60, '─'));
        sb.AppendLine();

        // 检测结果是否包含额外字段（聚合查询结果）
        var hasExtraFields = result.Events.Any(e => 
            e.Fields != null && 
            e.Fields.Keys.Any(k => !k.StartsWith('@')));

        if (hasExtraFields)
        {
            // 聚合查询结果，显示为表格格式
            foreach (var evt in result.Events)
            {
                if (evt.Fields != null)
                {
                    var fieldPairs = evt.Fields
                        .Where(kv => !kv.Key.StartsWith("@ptr"))
                        .Select(kv => $"{kv.Key}={kv.Value}");
                    sb.AppendLine(string.Join(" | ", fieldPairs));
                }
                else
                {
                    sb.AppendLine(evt.Message);
                }
            }
        }
        else
        {
            // 普通日志查询结果
            foreach (var evt in result.Events)
            {
                sb.AppendLine($"[{evt.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {evt.Message}");
            }
        }

        return sb.ToString();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}

#region Parameters

/// <summary>
/// CloudWatch Logs Insights 查询参数
/// </summary>
public class CloudWatchInsightsQueryParams
{
    /// <summary>
    /// 日志组名称列表（逗号或换行分隔）
    /// </summary>
    [Required]
    [Description("Log group names to query, separated by comma or newline. Example: '/aws/lambda/func1, /aws/lambda/func2'")]
    public string LogGroupNames { get; set; } = string.Empty;

    /// <summary>
    /// CloudWatch Logs Insights 查询语句
    /// </summary>
    [Required]
    [Description(@"CloudWatch Logs Insights query string. Examples:
- Basic: 'fields @timestamp, @message | sort @timestamp desc | limit 20'
- Filter: 'fields @timestamp, @message | filter @message like /ERROR/ | sort @timestamp desc'
- Parse: 'parse @message ""[*] *"" as level, msg | filter level = ""ERROR""'
- Stats: 'stats count(*) as errorCount by bin(5m) | filter @message like /ERROR/'
- Aggregation: 'stats count(*) as count, avg(duration) as avgDuration by serviceName'")]
    public string QueryString { get; set; } = string.Empty;

    /// <summary>
    /// 开始时间
    /// </summary>
    [Description("Start time for the query. Supports ISO 8601 format or relative time (e.g., '1h', '30m', '7d'). Defaults to 1 hour ago.")]
    public string? StartTime { get; set; }

    /// <summary>
    /// 结束时间
    /// </summary>
    [Description("End time for the query. Supports ISO 8601 format. Defaults to current time.")]
    public string? EndTime { get; set; }

    /// <summary>
    /// 返回结果数量限制
    /// </summary>
    [Description("Maximum number of results to return. Default is 1000, maximum is 10000.")]
    [Range(1, 10000)]
    public int? Limit { get; set; }
}

#endregion
