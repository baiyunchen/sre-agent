using Microsoft.AspNetCore.Mvc;
using SreAgent.Api.Models;
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
    private readonly ILogger<SreController> _logger;

    public SreController(
        IAgent agent,
        IContextStore contextStore,
        ITodoService todoService,
        ITokenEstimator tokenEstimator,
        ContextManagerOptions contextOptions,
        ILogger<SreController> logger)
    {
        _agent = agent;
        _contextStore = contextStore;
        _todoService = todoService;
        _tokenEstimator = tokenEstimator;
        _contextOptions = contextOptions;
        _logger = logger;
    }

    /// <summary>
    /// 聊天接口（支持新对话和追问）
    /// </summary>
    /// <remarks>
    /// - 不传 sessionId：开始新对话
    /// - 传 sessionId：在现有对话基础上追问
    /// </remarks>
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("收到请求: SessionId={SessionId}", request.SessionId);

        // 1. 准备上下文（包含 System Prompt + 用户输入）
        var context = await PrepareContextAsync(request, ct);

        // 2. 执行 Agent
        var result = await _agent.ExecuteAsync(context, cancellationToken: ct);

        // 3. 保存上下文
        await _contextStore.SaveAsync(result.Context.ExportSnapshot(context.SessionId), ct);

        // 4. 返回结果
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

        // 创建新对话上下文
        var context = DefaultContextManager.StartNew(
            input,
            _agent.Options.SystemPrompt,
            _tokenEstimator,
            options: _contextOptions);

        var result = await _agent.ExecuteAsync(context, cancellationToken: ct);

        // 获取生成的 Todo 列表
        var todos = await _todoService.GetAsync(context.SessionId);

        return Ok(new AnalyzeResponse
        {
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

    /// <summary>
    /// 准备对话上下文
    /// </summary>
    private async Task<IContextManager> PrepareContextAsync(ChatRequest request, CancellationToken ct)
    {
        // 追问：从存储恢复，然后添加新消息
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

        // 新对话：创建上下文，设置 System Prompt，添加用户输入
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
