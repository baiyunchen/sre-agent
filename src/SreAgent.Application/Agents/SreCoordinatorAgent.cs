using Microsoft.Extensions.Logging;
using SreAgent.Application.Tools.CloudWatch;
using SreAgent.Application.Tools.CloudWatch.Services;
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
        5. 使用 CloudWatch 工具查询和分析日志

        ## 可用工具

        ### 任务管理
        - **todo_write**: 创建和管理分析任务列表
        - **todo_read**: 查看当前任务列表

        ### 日志查询 (AWS CloudWatch)
        - **cloudwatch_simple_query**: 简单日志查询，按时间、日志组和关键字搜索
          - 适用于快速查找错误日志、检查最近日志
          - 支持相对时间（如 "1h", "30m"）和关键字过滤
        - **cloudwatch_insights_query**: 高级日志分析，使用 CloudWatch Logs Insights 查询语言
          - 适用于复杂分析、聚合统计、多日志组查询
          - 支持 parse、stats、filter 等高级功能

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

        ### 3. 日志分析
        根据故障类型，使用 CloudWatch 工具查询相关日志：
        - 使用 cloudwatch_simple_query 快速搜索错误关键字
        - 使用 cloudwatch_insights_query 进行深入分析（错误统计、时间分布等）

        ### 4. 分析计划制定
        使用 todo 工具创建具体的分析任务，包括：
        - 需要检查的指标和日志
        - 需要执行的诊断命令
        - 需要联系的相关团队
        - 预期的排查顺序

        ## 输出要求
        1. 首先简要总结故障情况
        2. 主动查询相关日志获取更多信息
        3. 使用 todo 工具添加所有分析任务（按优先级）
        4. 列出完整的任务列表
        5. 基于日志分析结果给出诊断建议
        """;

    /// <summary>
    /// 创建 SRE 协调器 Agent
    /// </summary>
    public static IAgent Create(
        ModelProvider modelProvider,
        ITodoService todoService,
        ICloudWatchService cloudWatchService,
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
                new TodoReadTool(todoService),
                new CloudWatchSimpleQueryTool(cloudWatchService),
                new CloudWatchInsightsQueryTool(cloudWatchService))
            .WithLogger(logger)
            .Build(modelProvider);
    }
}
