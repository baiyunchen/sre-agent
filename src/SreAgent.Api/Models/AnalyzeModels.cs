using System.ComponentModel.DataAnnotations;

namespace SreAgent.Api.Models;

/// <summary>
/// 故障分析请求
/// </summary>
public record AnalyzeRequest
{
    /// <summary>
    /// 告警标题
    /// </summary>
    [Required(ErrorMessage = "告警标题不能为空")]
    public required string Title { get; init; }

    /// <summary>
    /// 告警级别 (P0-P3)
    /// </summary>
    public string Severity { get; init; } = "P2";

    /// <summary>
    /// 告警时间
    /// </summary>
    public DateTime AlertTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// 受影响的服务
    /// </summary>
    [Required(ErrorMessage = "受影响服务不能为空")]
    public required string AffectedService { get; init; }

    /// <summary>
    /// 告警详细描述
    /// </summary>
    [Required(ErrorMessage = "告警描述不能为空")]
    public required string Description { get; init; }

    /// <summary>
    /// 附加信息
    /// </summary>
    public string? AdditionalInfo { get; init; }
}

/// <summary>
/// 故障分析响应
/// </summary>
public record AnalyzeResponse
{
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 分析结果
    /// </summary>
    public string? Analysis { get; init; }

    /// <summary>
    /// 错误信息
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// 生成的任务列表
    /// </summary>
    public List<TaskItem> Tasks { get; init; } = [];

    /// <summary>
    /// Token 使用统计
    /// </summary>
    public TokenUsageInfo TokenUsage { get; init; } = new();

    /// <summary>
    /// Agent 迭代次数
    /// </summary>
    public int IterationCount { get; init; }
}

/// <summary>
/// 任务项
/// </summary>
public record TaskItem
{
    /// <summary>
    /// 任务 ID
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// 任务描述
    /// </summary>
    public string Task { get; init; } = string.Empty;

    /// <summary>
    /// 优先级
    /// </summary>
    public string Priority { get; init; } = "Medium";

    /// <summary>
    /// 状态
    /// </summary>
    public string Status { get; init; } = "Pending";

    /// <summary>
    /// 备注
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Token 使用信息
/// </summary>
public record TokenUsageInfo
{
    /// <summary>
    /// 输入 Token 数
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// 输出 Token 数
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// 总 Token 数
    /// </summary>
    public int TotalTokens { get; init; }
}
