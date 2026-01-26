using Microsoft.Extensions.Logging;
using SreAgent.Application.Tools.Todo;
using SreAgent.Application.Tools.Todo.Services;
using SreAgent.Framework.Agents;
using SreAgent.Framework.Options;
using SreAgent.Framework.Providers;

namespace SreAgent.Application.Agents;

/// <summary>
/// SRE 故障分析协调器 Agent
/// 负责接收故障告警，分析故障类型，制定分析计划
/// </summary>
public static class SreCoordinatorAgent
{
    private const string AgentId = "sre-coordinator";
    private const string AgentName = "SRE 故障分析协调器";
    private const string AgentDescription = "负责分析线上故障告警，制定故障分析和排查计划";
    
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

        ## 注意事项
        - 保持冷静和系统化思维
        - 优先排查最可能的原因
        - 考虑故障的连锁反应
        - 记录所有分析步骤以便复盘
        """;
    
    /// <summary>
    /// 创建 SRE 协调器 Agent 实例
    /// </summary>
    /// <param name="modelProvider">模型提供者（默认使用智谱）</param>
    /// <param name="todoService">Todo 服务实例（可选，默认创建新实例）</param>
    /// <param name="logger">日志记录器（可选）</param>
    /// <returns>配置好的 ToolLoopAgent</returns>
    public static ToolLoopAgent Create(
        ModelProvider? modelProvider = null,
        ITodoService? todoService = null,
        ILogger<ToolLoopAgent>? logger = null)
    {
        // 默认使用智谱模型
        modelProvider ??= ModelProvider.Zhipu();
        todoService ??= new TodoService();
        
        var todoWriteTool = new TodoWriteTool(todoService);
        var todoReadTool = new TodoReadTool(todoService);
        
        var options = new AgentOptions
        {
            SystemPrompt = SystemPrompt,
            ModelCapability = ModelCapability.Medium,  // 使用中等能力模型，性价比高
            MaxIterations = 15,  // 允许足够的迭代次数来完成任务规划
            Temperature = 0.3,   // 较低温度，保持输出稳定
            Tools = [todoWriteTool, todoReadTool]
        };
        
        return new ToolLoopAgent(
            AgentId,
            AgentName,
            AgentDescription,
            modelProvider,
            options,
            logger);
    }
    
    /// <summary>
    /// 创建带自定义配置的 SRE 协调器 Agent
    /// </summary>
    public static ToolLoopAgent Create(
        ModelProvider modelProvider,
        AgentOptions options,
        ILogger<ToolLoopAgent>? logger = null)
    {
        // 确保 System Prompt 被设置
        options.SystemPrompt ??= SystemPrompt;
        
        return new ToolLoopAgent(
            AgentId,
            AgentName,
            AgentDescription,
            modelProvider,
            options,
            logger);
    }
}
