using Microsoft.Extensions.Logging;
using SreAgent.Application.Tools.Todo;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Providers;

namespace SreAgent.Application.Agents;

/// <summary>
/// SRE 故障分析协调器 Agent
/// 负责接收故障告警，分析故障类型，制定分析计划
/// </summary>
public static class SreCoordinatorAgent
{
    public const string AgentId = "sre-coordinator";
    public const string AgentName = "SRE 故障分析协调器";
    public const string AgentDescription = "负责分析线上故障告警，制定故障分析和排查计划";

    private const string SystemPrompt = """
        你是一个专业的 SRE（Site Reliability Engineer）故障分析协调器。

        ## 你的职责
        1. 接收和分析线上故障告警信息
        2. 识别故障类型和可能的影响范围
        3. 制定系统化的故障分析计划
        4. 使用 todo 工具记录分析步骤

        ## 故障分析框架
        当收到故障告警时，请按以下框架进行分析：

        ### 1. 故障识别
        - 故障类型（服务不可用、性能下降、数据异常、安全事件等）
        - 影响范围（用户量、业务线、地域等）
        - 紧急程度（P0-P3）

        ### 2. 初步诊断方向
        - 基础设施层面（网络、DNS、负载均衡、CDN）
        - 应用层面（服务状态、错误率、响应时间）
        - 数据层面（数据库、缓存、消息队列）
        - 外部依赖（第三方服务、API）

        ### 3. 分析计划制定
        使用 todo 工具创建具体的分析任务，包括：
        - 需要检查的指标和日志
        - 需要执行的诊断命令
        - 需要联系的相关团队
        - 预期的排查顺序

        ## 输出要求
        1. 首先简要总结故障情况
        2. 使用 todo 工具添加所有分析任务（按优先级）
        3. 列出完整的任务列表
        4. 给出下一步建议
        """;

    /// <summary>
    /// 创建 SRE 协调器 Agent
    /// </summary>
    public static IAgent Create(
        ModelProvider modelProvider,
        ITodoService todoService,
        ILogger<ToolLoopAgent>? logger = null)
    {
        return AgentBuilder.Create(AgentId)
            .WithName(AgentName)
            .WithDescription(AgentDescription)
            .WithSystemPrompt(SystemPrompt)
            .WithModelCapability(ModelCapability.Medium)
            .WithMaxIterations(15)
            .WithTemperature(0.3)
            .WithTools(
                new TodoWriteTool(todoService),
                new TodoReadTool(todoService))
            .WithLogger(logger)
            .Build(modelProvider);
    }
}
