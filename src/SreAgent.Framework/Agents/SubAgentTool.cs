using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Contexts;
using SreAgent.Framework.Results;

namespace SreAgent.Framework.Agents;

/// <summary>
/// 将子 Agent 包装为 Tool
/// 允许主 Agent 调用子 Agent 处理特定任务
/// </summary>
public class SubAgentTool : ToolBase<SubAgentTool.Parameters>
{
    private readonly IAgent _subAgent;
    private readonly ITokenEstimator _tokenEstimator;

    public SubAgentTool(IAgent subAgent, ITokenEstimator? tokenEstimator = null)
    {
        _subAgent = subAgent;
        _tokenEstimator = tokenEstimator ?? new SimpleTokenEstimator();
    }

    public override string Name => _subAgent.Id;
    public override string Summary => _subAgent.Description;
    public override string Description => BuildDescription();
    public override string Category => "SubAgent";

    protected override async Task<ToolResult> ExecuteAsync(
        Parameters parameters,
        ToolExecutionContext toolContext,
        CancellationToken cancellationToken)
    {
        // 构建子 Agent 的输入（注入父上下文摘要）
        var input = BuildInput(parameters.Task, toolContext.ParentContext);

        // 创建子 Agent 的上下文
        var context = DefaultContextManager.StartNew(
            input,
            _subAgent.Options.SystemPrompt,
            _tokenEstimator,
            toolContext.SessionId);

        var result = await _subAgent.ExecuteAsync(context, toolContext.Variables, cancellationToken);

        return result.IsSuccess
            ? ToolResult.Success(result.Output ?? "子 Agent 执行完成")
            : ToolResult.Failure(result.Error?.Message ?? "子 Agent 执行失败", "SUB_AGENT_ERROR");
    }

    private static string BuildInput(string task, IContextManager? parentContext)
    {
        var parentSummary = parentContext?.GenerateSummary();
        return string.IsNullOrEmpty(parentSummary)
            ? task
            : $"[父任务背景]\n{parentSummary}\n\n[当前任务]\n{task}";
    }

    private string BuildDescription()
    {
        return $"""
            调用子 Agent: {_subAgent.Name}
            
            {_subAgent.Description}
            
            使用场景：当需要 {_subAgent.Name} 的专业能力来处理特定任务时调用此工具。
            """;
    }

    /// <summary>子 Agent 工具参数</summary>
    public class Parameters
    {
        /// <summary>需要子 Agent 处理的任务描述</summary>
        [Required]
        [Description("需要子 Agent 处理的任务描述")]
        public string Task { get; set; } = string.Empty;
    }
}
