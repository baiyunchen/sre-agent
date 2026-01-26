using Microsoft.AspNetCore.Mvc;
using SreAgent.Api.Models;
using SreAgent.Application.Agents;
using SreAgent.Application.Tools;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Providers;

namespace SreAgent.Api.Controllers;

/// <summary>
/// SRE 故障分析控制器
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SreController : ControllerBase
{
    private readonly TodoTool _todoTool;
    private readonly ModelProvider _modelProvider;
    private readonly ILogger<SreController> _logger;
    private readonly ILogger<ToolLoopAgent> _agentLogger;

    public SreController(
        TodoTool todoTool,
        ModelProvider modelProvider,
        ILogger<SreController> logger,
        ILogger<ToolLoopAgent> agentLogger)
    {
        _todoTool = todoTool;
        _modelProvider = modelProvider;
        _logger = logger;
        _agentLogger = agentLogger;
    }

    /// <summary>
    /// 分析故障告警并生成分析计划
    /// </summary>
    /// <param name="request">故障告警请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分析结果和任务列表</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalyzeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalyzeResponse>> Analyze(
        [FromBody] AnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "收到故障分析请求: {Title}, 级别: {Severity}, 服务: {Service}",
            request.Title, request.Severity, request.AffectedService);

        // 创建 SRE 协调器 Agent
        var agent = SreCoordinatorAgent.Create(_modelProvider, _todoTool, _agentLogger);

        // 构建故障描述
        var input = $"""
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

        var context = new AgentExecutionContext
        {
            Input = input
        };

        // 执行 Agent
        var result = await agent.ExecuteAsync(context, cancellationToken);

        // 获取生成的 Todo 列表
        var todos = _todoTool.GetTodos(context.SessionId);

        _logger.LogInformation(
            "故障分析完成: 成功={Success}, 迭代次数={Iterations}, Token使用={Tokens}",
            result.IsSuccess, result.IterationCount, result.TokenUsage.TotalTokens);

        return Ok(new AnalyzeResponse
        {
            Success = result.IsSuccess,
            Analysis = result.Output,
            Error = result.Error?.Message,
            Tasks = todos.Select(t => new TaskItem
            {
                Id = t.Id,
                Task = t.Task,
                Priority = t.Priority.ToString(),
                Status = t.Status.ToString(),
                Notes = t.Notes
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
}
