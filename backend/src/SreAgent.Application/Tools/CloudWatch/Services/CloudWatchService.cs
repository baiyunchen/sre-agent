using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;
using Microsoft.Extensions.Logging;

namespace SreAgent.Application.Tools.CloudWatch.Services;

/// <summary>
/// CloudWatch 日志查询服务实现
/// 支持 AWS SSO、环境变量、配置文件等多种认证方式
/// </summary>
public class CloudWatchService : ICloudWatchService, IDisposable
{
    private readonly AmazonCloudWatchLogsClient _client;
    private readonly ILogger<CloudWatchService>? _logger;
    private readonly CloudWatchServiceOptions _options;

    public CloudWatchService(
        CloudWatchServiceOptions? options = null,
        ILogger<CloudWatchService>? logger = null)
    {
        _options = options ?? new CloudWatchServiceOptions();
        _logger = logger;
        _client = CreateClient();
    }

    private AmazonCloudWatchLogsClient CreateClient()
    {
        var config = new AmazonCloudWatchLogsConfig();
        
        // 设置区域
        if (!string.IsNullOrEmpty(_options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_options.Region);
        }
        
        // 设置自定义端点（用于 LocalStack 等本地测试）
        if (!string.IsNullOrEmpty(_options.ServiceUrl))
        {
            config.ServiceURL = _options.ServiceUrl;
        }

        // 使用默认凭证链（支持 SSO、环境变量、配置文件等）
        // AWS SDK 会自动按以下顺序查找凭证：
        // 1. 环境变量 (AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY)
        // 2. AWS SSO 缓存的凭证
        // 3. 共享凭证文件 (~/.aws/credentials)
        // 4. IAM 角色（EC2/ECS/Lambda 环境）
        return new AmazonCloudWatchLogsClient(config);
    }

    /// <inheritdoc />
    public async Task<CloudWatchQueryResult> SimpleQueryAsync(
        string logGroupName,
        DateTime startTime,
        DateTime endTime,
        string? filterPattern = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation(
                "执行 CloudWatch 简单查询: LogGroup={LogGroup}, StartTime={StartTime}, EndTime={EndTime}, Pattern={Pattern}",
                logGroupName, startTime, endTime, filterPattern);

            var request = new FilterLogEventsRequest
            {
                LogGroupName = logGroupName,
                StartTime = ToUnixTimestamp(startTime),
                EndTime = ToUnixTimestamp(endTime),
                Limit = Math.Min(limit, 10000) // AWS 最大限制为 10000
            };

            if (!string.IsNullOrWhiteSpace(filterPattern))
            {
                request.FilterPattern = filterPattern;
            }

            var events = new List<LogEvent>();
            string? nextToken = null;

            do
            {
                request.NextToken = nextToken;
                var response = await _client.FilterLogEventsAsync(request, cancellationToken);

                events.AddRange(response.Events.Select(e => new LogEvent
                {
                    Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(e.Timestamp).UtcDateTime,
                    Message = e.Message,
                    LogStreamName = e.LogStreamName,
                    EventId = e.EventId
                }));

                nextToken = response.NextToken;

                // 如果已达到限制数量，停止分页
                if (events.Count >= limit)
                {
                    break;
                }
            } while (!string.IsNullOrEmpty(nextToken));

            _logger?.LogInformation("CloudWatch 简单查询完成，返回 {Count} 条记录", events.Count);

            return CloudWatchQueryResult.Success(
                events.Take(limit).ToList(),
                hasMoreResults: !string.IsNullOrEmpty(nextToken) || events.Count > limit);
        }
        catch (ResourceNotFoundException ex)
        {
            _logger?.LogWarning(ex, "日志组不存在: {LogGroup}", logGroupName);
            return CloudWatchQueryResult.Failure($"日志组 '{logGroupName}' 不存在");
        }
        catch (AmazonCloudWatchLogsException ex)
        {
            _logger?.LogError(ex, "CloudWatch 查询失败: {ErrorCode}", ex.ErrorCode);
            return CloudWatchQueryResult.Failure($"CloudWatch 查询失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CloudWatch 查询发生未知错误");
            return CloudWatchQueryResult.Failure($"查询失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<CloudWatchQueryResult> InsightsQueryAsync(
        IEnumerable<string> logGroupNames,
        string queryString,
        DateTime startTime,
        DateTime endTime,
        int limit = 1000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logGroups = logGroupNames.ToList();
            _logger?.LogInformation(
                "执行 CloudWatch Insights 查询: LogGroups={LogGroups}, Query={Query}",
                string.Join(", ", logGroups), queryString);

            // 启动查询
            var startRequest = new StartQueryRequest
            {
                LogGroupNames = logGroups,
                QueryString = queryString,
                StartTime = ToUnixTimestamp(startTime),
                EndTime = ToUnixTimestamp(endTime),
                Limit = Math.Min(limit, 10000)
            };

            var startResponse = await _client.StartQueryAsync(startRequest, cancellationToken);
            var queryId = startResponse.QueryId;

            _logger?.LogDebug("Insights 查询已启动: QueryId={QueryId}", queryId);

            // 轮询查询结果
            var getResultsRequest = new GetQueryResultsRequest { QueryId = queryId };
            GetQueryResultsResponse? results = null;
            var maxWaitTime = TimeSpan.FromMinutes(5);
            var pollInterval = TimeSpan.FromMilliseconds(500);
            var elapsed = TimeSpan.Zero;

            while (elapsed < maxWaitTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                results = await _client.GetQueryResultsAsync(getResultsRequest, cancellationToken);

                if (results.Status == QueryStatus.Complete ||
                    results.Status == QueryStatus.Failed ||
                    results.Status == QueryStatus.Cancelled)
                {
                    break;
                }

                await Task.Delay(pollInterval, cancellationToken);
                elapsed += pollInterval;
                
                // 逐渐增加轮询间隔
                if (pollInterval < TimeSpan.FromSeconds(2))
                {
                    pollInterval = TimeSpan.FromMilliseconds(Math.Min(pollInterval.TotalMilliseconds * 1.5, 2000));
                }
            }

            if (results == null)
            {
                return CloudWatchQueryResult.Failure("查询超时");
            }

            if (results.Status == QueryStatus.Failed)
            {
                return CloudWatchQueryResult.Failure("查询执行失败");
            }

            if (results.Status == QueryStatus.Cancelled)
            {
                return CloudWatchQueryResult.Failure("查询已取消");
            }

            // 转换结果
            var events = results.Results.Select(row =>
            {
                var fields = row.ToDictionary(f => f.Field, f => f.Value);
                
                // 尝试解析时间戳
                DateTime timestamp = DateTime.UtcNow;
                if (fields.TryGetValue("@timestamp", out var ts))
                {
                    DateTime.TryParse(ts, out timestamp);
                }

                // 获取消息
                var message = fields.GetValueOrDefault("@message") ?? 
                              fields.GetValueOrDefault("message") ?? 
                              string.Join(" | ", fields.Select(kv => $"{kv.Key}={kv.Value}"));

                return new LogEvent
                {
                    Timestamp = timestamp,
                    Message = message,
                    LogStreamName = fields.GetValueOrDefault("@logStream"),
                    Fields = fields
                };
            }).ToList();

            var statistics = results.Statistics != null
                ? new QueryStatistics
                {
                    RecordsScanned = (long)results.Statistics.RecordsScanned,
                    RecordsMatched = (long)results.Statistics.RecordsMatched,
                    BytesScanned = (long)results.Statistics.BytesScanned
                }
                : null;

            _logger?.LogInformation(
                "CloudWatch Insights 查询完成，返回 {Count} 条记录，扫描 {Scanned} 条",
                events.Count, statistics?.RecordsScanned ?? 0);

            return CloudWatchQueryResult.Success(events, statistics);
        }
        catch (MalformedQueryException ex)
        {
            _logger?.LogWarning(ex, "查询语句格式错误");
            return CloudWatchQueryResult.Failure($"查询语句格式错误: {ex.Message}");
        }
        catch (ResourceNotFoundException ex)
        {
            _logger?.LogWarning(ex, "日志组不存在");
            return CloudWatchQueryResult.Failure($"一个或多个日志组不存在: {ex.Message}");
        }
        catch (AmazonCloudWatchLogsException ex)
        {
            _logger?.LogError(ex, "CloudWatch Insights 查询失败: {ErrorCode}", ex.ErrorCode);
            return CloudWatchQueryResult.Failure($"CloudWatch Insights 查询失败: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "CloudWatch Insights 查询发生未知错误");
            return CloudWatchQueryResult.Failure($"查询失败: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LogGroupInfo>> ListLogGroupsAsync(
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger?.LogInformation("获取日志组列表: Prefix={Prefix}", prefix);

            var request = new DescribeLogGroupsRequest();
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                request.LogGroupNamePrefix = prefix;
            }

            var logGroups = new List<LogGroupInfo>();
            string? nextToken = null;

            do
            {
                request.NextToken = nextToken;
                var response = await _client.DescribeLogGroupsAsync(request, cancellationToken);

                logGroups.AddRange(response.LogGroups.Select(lg => new LogGroupInfo
                {
                    Name = lg.LogGroupName,
                    Arn = lg.Arn,
                    CreationTime = lg.CreationTime,
                    RetentionInDays = lg.RetentionInDays,
                    StoredBytes = lg.StoredBytes
                }));

                nextToken = response.NextToken;
            } while (!string.IsNullOrEmpty(nextToken));

            _logger?.LogInformation("获取到 {Count} 个日志组", logGroups.Count);
            return logGroups;
        }
        catch (AmazonCloudWatchLogsException ex)
        {
            _logger?.LogError(ex, "获取日志组列表失败: {ErrorCode}", ex.ErrorCode);
            throw;
        }
    }

    private static long ToUnixTimestamp(DateTime dateTime)
    {
        return new DateTimeOffset(dateTime.Kind == DateTimeKind.Utc 
            ? dateTime 
            : dateTime.ToUniversalTime()).ToUnixTimeMilliseconds();
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// CloudWatch 服务配置选项
/// </summary>
public class CloudWatchServiceOptions
{
    /// <summary>
    /// AWS 区域（如 ap-northeast-1）
    /// 如果不设置，将使用默认区域配置
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 自定义服务 URL（用于 LocalStack 等本地测试环境）
    /// </summary>
    public string? ServiceUrl { get; set; }

    /// <summary>
    /// AWS Profile 名称（用于指定特定的凭证配置）
    /// </summary>
    public string? ProfileName { get; set; }
}
