using Microsoft.Extensions.Logging;
using SreAgent.Application.Services;
using SreAgent.Application.Tools.CloudWatch;
using SreAgent.Application.Tools.CloudWatch.Services;
using SreAgent.Application.Tools.DiagnosticData;
using SreAgent.Application.Tools.KnowledgeBase;
using SreAgent.Application.Tools.KnowledgeBase.Services;
using SreAgent.Application.Tools.Todo;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Abstractions;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Providers;
using SreAgent.Repository;

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
        2. **首先查询 Knowledge Base 获取相关的 Playbook**
        3. 识别故障类型和可能的影响范围
        4. 制定系统化的故障分析计划
        5. 使用 todo 工具记录分析步骤
        6. 使用 CloudWatch 工具查询和分析日志

        ## 可用工具

        ### 知识库查询 (首选)
        - **knowledge_base_query**: 查询 SRE Playbook 知识库
          - **收到告警时首先使用此工具**查找相关的 Playbook
          - 包含各服务的故障排查指南、CloudWatch 查询示例、常见问题解决方案
          - 查询时优先使用告警标题进行查询，但需要将环境信息抹去（比如告警标题的最前面或最后面可能会包含"-dev"），然后如果查询不到合适的话可以考虑使用服务名称进行泛华查询
          - 如果上述两种方式都无法查询到，那就不要再查询了，需要自行探索解决方案

        ### 任务管理
        - **todo_write**: 创建和管理分析任务列表
        - **todo_read**: 查看当前任务列表

        ### 日志查询 (AWS CloudWatch)
        - **cloudwatch_simple_query**: 简单日志查询，按时间、日志组和关键字搜索
          - 适用于快速查找错误日志、检查最近日志
          - 支持相对时间（如 "1h", "30m"）和关键字过滤
          - 大量结果会自动存入诊断数据库，只返回摘要
        - **cloudwatch_insights_query**: 高级日志分析，使用 CloudWatch Logs Insights 查询语言
          - 适用于复杂分析、聚合统计、多日志组查询
          - 支持 parse、stats、filter 等高级功能

        ### 诊断数据查询（从数据库中检索之前存储的大量日志/指标）
        - **search_diagnostic_data**: 按关键字、严重级别、来源、时间范围搜索已存储的诊断数据
        - **query_diagnostic_data**: 对诊断数据执行受限 SQL 查询（仅 SELECT，自动注入 session_id）
        - **get_diagnostic_summary**: 获取当前会话的诊断数据汇总统计

        ## 故障分析框架
        当收到故障告警时，请按以下框架进行分析：

        ### 1. 查询 Playbook（首要步骤）
        - 使用 knowledge_base_query 工具查询与告警相关的 Playbook
        - 根据告警名称、服务名称或错误类型进行查询
        - Playbook 包含详细的排查步骤和解决方案

        ### 2. 故障识别
        - 故障类型（服务不可用、性能下降、数据异常、安全事件等）
        - 影响范围（用户量、业务线、地域等）
        - 紧急程度（P0-P3）

        ### 3. 初步诊断方向
        - 基础设施层面（网络、DNS、负载均衡、CDN）
        - 应用层面（服务状态、错误率、响应时间）
        - 数据层面（数据库、缓存、消息队列）
        - 外部依赖（第三方服务、API）

        ### 4. 日志分析
        根据 Playbook 指导和故障类型，使用 CloudWatch 工具查询相关日志：
        - 使用 cloudwatch_simple_query 快速搜索错误关键字
        - 使用 cloudwatch_insights_query 进行深入分析（错误统计、时间分布等）
        - 参考 Playbook 中的日志查询示例

        ### 5. 分析计划制定
        使用 todo 工具创建具体的分析任务，包括：
        - Playbook 中建议的排查步骤
        - 需要检查的指标和日志
        - 需要执行的诊断命令
        - 需要联系的相关团队
        - 预期的排查顺序

        ## 输出要求
        1. **首先查询 Knowledge Base** 获取相关 Playbook
        2. 总结故障情况和 Playbook 建议
        3. 根据 Playbook 指导查询相关日志
        4. 使用 todo 工具添加所有分析任务（按优先级）
        5. 列出完整的任务列表
        6. 基于 Playbook 和日志分析结果给出诊断建议
        """;

    /// <summary>
    /// 创建 SRE 协调器 Agent
    /// </summary>
    public static IAgent Create(
        ModelProvider modelProvider,
        ITodoService todoService,
        ICloudWatchService cloudWatchService,
        IKnowledgeBaseService? knowledgeBaseService = null,
        IDiagnosticDataService? diagnosticDataService = null,
        AppDbContext? dbContext = null,
        ILogger<ToolLoopAgent>? logger = null)
    {
        var tools = new List<ITool>
        {
            new TodoWriteTool(todoService),
            new TodoReadTool(todoService),
            new CloudWatchSimpleQueryTool(cloudWatchService, diagnosticDataService),
            new CloudWatchInsightsQueryTool(cloudWatchService, diagnosticDataService)
        };

        // 如果配置了 Knowledge Base 服务，则添加 Knowledge Base 查询工具
        if (knowledgeBaseService != null)
        {
            tools.Insert(0, new KnowledgeBaseQueryTool(knowledgeBaseService));
        }

        // 如果配置了诊断数据服务，则添加诊断数据查询工具
        if (diagnosticDataService != null)
        {
            tools.Add(new SearchDiagnosticDataTool(diagnosticDataService));
            tools.Add(new GetDiagnosticSummaryTool(diagnosticDataService));
        }
        if (dbContext != null)
        {
            tools.Add(new QueryDiagnosticDataTool(dbContext));
        }

        return AgentBuilder.Create(AgentId)
            .WithName(AgentName)
            .WithDescription(AgentDescription)
            .WithSystemPrompt(SystemPrompt)
            .WithModelCapability(ModelCapability.Medium)
            .WithMaxIterations(15)
            .WithTemperature(0.3)
            .WithTools(tools.ToArray())
            .WithLogger(logger)
            .Build(modelProvider);
    }
}
