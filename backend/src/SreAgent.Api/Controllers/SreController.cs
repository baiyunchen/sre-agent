using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SreAgent.Api.Models;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;

namespace SreAgent.Api.Controllers;

/// <summary>
/// SRE Agent 控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SreController : ControllerBase
{
    private readonly IAgent _agent;
    private readonly IContextStore _contextStore;
    private readonly ITodoService _todoService;
    private readonly ITokenEstimator _tokenEstimator;
    private readonly ContextManagerOptions _contextOptions;
    private readonly IExecutionTracker _executionTracker;
    private readonly IAuditService _auditService;
    private readonly ILogger<SreController> _logger;

    public SreController(
        IAgent agent,
        IContextStore contextStore,
        ITodoService todoService,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions contextOptions,
        IExecutionTracker executionTracker,
        IAuditService auditService,
        ILogger<SreController> logger)
    {
        _agent = agent;
        _contextStore = contextStore;
        _todoService = todoService;
        _tokenEstimator = tokenEstimator;
        _contextOptions = contextOptions;
        _executionTracker = executionTracker;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 聊天接口（支持新对话和追问）
    /// </summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("收到请求: SessionId={SessionId}", request.SessionId);

        var context = await PrepareContextAsync(request, ct);

        var variables = new Dictionary<string, object>
        {
            [IExecutionTracker.VariableKey] = _executionTracker
        };

        var result = await _agent.ExecuteAsync(context, variables, ct);

        await _contextStore.SaveAsync(result.Context.ExportSnapshot(context.SessionId), ct);

        return Ok(new ChatResponse
        {
            SessionId = context.SessionId,
            Output = result.Output,
            IsSuccess = result.IsSuccess,
            Error = result.Error?.Message,
            TokenUsage = new TokenUsageInfo
            {
                PromptTokens = result.TokenUsage.PromptTokens,
                CompletionTokens = result.TokenUsage.CompletionTokens,
                TotalTokens = result.TokenUsage.TotalTokens
            }
        });
    }

    /// <summary>
    /// 分析故障告警（旧接口，保持兼容）
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        [FromBody] AnalyzeRequest request,
        CancellationToken ct)
    {
        var input = BuildAlertInput(request);
        var startedAt = DateTime.UtcNow;

        var context = DefaultContextManager.StartNew(
            input,
            _agent.Options.SystemPrompt,
            _tokenEstimator,
            options: _contextOptions);

        var alertMetadata = BuildAlertMetadata(request, context.SessionId, startedAt);

        // Save initial context to create the session in DB (with alert fields and Running status)
        await _contextStore.SaveAsync(
            context.ExportSnapshot(context.SessionId, alertMetadata), ct);

        await _auditService.LogAsync(context.SessionId, "SessionStarted",
            $"Analysis session started for alert: {request.Title}",
            new { request.Title, request.Severity, request.AffectedService },
            "system", null, ct);

        var variables = new Dictionary<string, object>
        {
            [IExecutionTracker.VariableKey] = _executionTracker
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await _agent.ExecuteAsync(context, variables, ct);
        stopwatch.Stop();

        var completedAt = DateTime.UtcNow;
        var sessionStatus = result.IsSuccess ? "Completed" : "Failed";

        var completionMetadata = new Dictionary<string, object>(alertMetadata)
        {
            ["status"] = sessionStatus,
            ["completed_at"] = completedAt,
            ["current_step"] = result.IterationCount,
            ["diagnosis_summary"] = result.Output ?? string.Empty,
            ["prompt_tokens"] = result.TokenUsage.PromptTokens,
            ["completion_tokens"] = result.TokenUsage.CompletionTokens,
            ["total_tokens"] = result.TokenUsage.TotalTokens
        };

        await _contextStore.SaveAsync(
            result.Context.ExportSnapshot(context.SessionId, completionMetadata), ct);

        await _auditService.LogAsync(context.SessionId,
            result.IsSuccess ? "SessionCompleted" : "SessionFailed",
            result.IsSuccess
                ? $"Analysis completed in {stopwatch.ElapsedMilliseconds}ms"
                : $"Analysis failed: {result.Error?.Message}",
            new { durationMs = stopwatch.ElapsedMilliseconds, iterationCount = result.IterationCount },
            "system", null, ct);

        var todos = await _todoService.GetAsync(context.SessionId);

        return Ok(new AnalyzeResponse
        {
            SessionId = context.SessionId,
            Success = result.IsSuccess,
            Analysis = result.Output,
            Error = result.Error?.Message,
            Tasks = todos.Select(t => new TaskItem
            {
                Id = t.Id,
                Task = t.Content,
                Priority = t.Priority.ToString(),
                Status = t.Status.ToString()
            }).ToList(),
            TokenUsage = new TokenUsageInfo
            {
                PromptTokens = result.TokenUsage.PromptTokens,
                CompletionTokens = result.TokenUsage.CompletionTokens,
                TotalTokens = result.TokenUsage.TotalTokens
            },
            IterationCount = result.IterationCount
        });
    }

    private Dictionary<string, object> BuildAlertMetadata(AnalyzeRequest request, Guid sessionId, DateTime startedAt)
    {
        var alertSource = InferAlertSource(request.AdditionalInfo);
        var alertSeverity = NormalizeSeverity(request.Severity);

        return new Dictionary<string, object>
        {
            ["alert_name"] = request.Title,
            ["alert_id"] = $"alert-{sessionId:N}"[..36],
            ["service_name"] = request.AffectedService,
            ["alert_source"] = alertSource,
            ["alert_severity"] = alertSeverity,
            ["alert_data"] = new
            {
                title = request.Title,
                source = alertSource,
                severity = alertSeverity,
                alertTime = request.AlertTime,
                affectedService = request.AffectedService,
                description = request.Description,
                additionalInfo = request.AdditionalInfo
            },
            ["status"] = "Running",
            ["started_at"] = startedAt,
            ["current_agent_id"] = _agent.Id
        };
    }

    private static string InferAlertSource(string? additionalInfo)
    {
        if (string.IsNullOrWhiteSpace(additionalInfo))
            return "CloudWatch";

        if (additionalInfo.Contains("Prometheus", StringComparison.OrdinalIgnoreCase))
            return "Prometheus";

        if (additionalInfo.Contains("Slack", StringComparison.OrdinalIgnoreCase))
            return "Slack Manual";

        return "CloudWatch";
    }

    private static string NormalizeSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity))
            return "Warning";

        return severity.Trim().ToUpperInvariant() switch
        {
            "P0" => "Critical",
            "P1" => "Critical",
            "P2" => "Warning",
            "P3" => "Info",
            _ => severity
        };
    }

    private async Task<IContextManager> PrepareContextAsync(ChatRequest request, CancellationToken ct)
    {
        if (request.SessionId.HasValue)
        {
            var snapshot = await _contextStore.GetAsync(request.SessionId.Value, ct);
            if (snapshot != null)
            {
                var context = DefaultContextManager.FromSnapshot(snapshot, _tokenEstimator, _contextOptions);
                context.AddUserMessage(request.Message);
                return context;
            }
        }

        return DefaultContextManager.StartNew(
            request.Message,
            _agent.Options.SystemPrompt,
            _tokenEstimator,
            options: _contextOptions);
    }

    private static string BuildAlertInput(AnalyzeRequest request)
    {
        return $"""
            ## 故障告警

            **告警标题**: {request.Title}
            **告警级别**: {request.Severity}
            **告警时间**: {request.AlertTime:yyyy-MM-dd HH:mm:ss}
            **受影响服务**: {request.AffectedService}

            **告警详情**:
            {request.Description}

            **附加信息**:
            {request.AdditionalInfo ?? "无"}

            请分析此故障并制定分析计划。
            """;
    }
}
