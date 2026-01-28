# 多 Agent 架构详解

## 1. 架构概述

### 1.1 分层设计

多 Agent 架构分为两层：

| 层次 | 说明 | 内容 |
|------|------|------|
| Agent Framework | 通用框架层 | `ToolLoopAgent`、`AgentAsTool` 等基础能力 |
| Business Agents | 业务实现层 | `DiagnosticAgent`、`LogAnalysisAgent` 等具体实现 |

**关键原则**：Agent Framework 不包含任何具体业务 Agent，只提供构建 Agent 的基础设施。

### 1.2 为什么需要多 Agent

单一 Agent 处理复杂 SRE 任务时面临的问题：

| 问题 | 影响 | 多 Agent 解决方案 |
|------|------|------------------|
| 上下文过长 | Token 消耗高，响应慢 | 每个 Agent 只关注特定领域 |
| 职责不清 | 决策质量下降 | 专业化分工 |
| 工具过多 | 选择困难，容易出错 | 每个 Agent 配备专属工具 |
| 难以扩展 | 新增能力需改动核心逻辑 | 新增 Agent 即可 |
| 难以调试 | 问题定位困难 | 各 Agent 独立可观测 |

---

## 2. Framework 层：基础能力

### 2.1 ToolLoopAgent

Framework 提供的核心基础 Agent 实现，实现标准的 ReAct 模式。

```csharp
/// <summary>
/// 工具循环 Agent - Framework 提供的基础实现
/// 实现标准的 ReAct 模式：Think -> Act -> Observe 循环
/// </summary>
public class ToolLoopAgent : IAgent
{
    public string Id { get; }
    public string Name { get; }
    public string Description { get; }
    public AgentOptions Options { get; }
    
    public ToolLoopAgent(
        string id,
        string name,
        string description,
        ModelProvider modelProvider,
        AgentOptions options,
        ILogger<ToolLoopAgent>? logger = null);
    
    public async Task<AgentResult> ExecuteAsync(
        IContextManager context,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 配置上下文 Token 限制
        ConfigureContextTokenLimit(context);
        
        // 2. 运行主循环
        return await RunMainLoop(context, variables ?? new Dictionary<string, object>(), cancellationToken);
    }
    
    private async Task<AgentResult> RunMainLoop(
        IContextManager context,
        IReadOnlyDictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var totalTokenUsage = new TokenUsage();
        
        for (var iteration = 0; iteration < Options.MaxIterations; iteration++)
        {
            // 调用 LLM
            var (response, tokenUsage) = await _llmCaller.CallAsync(
                chatClient,
                context.GetChatMessages(),
                Options.Tools,
                Options.Temperature,
                Options.MaxTokens,
                cancellationToken);
            
            totalTokenUsage += tokenUsage;
            context.RecordTokenUsage(tokenUsage.PromptTokens, tokenUsage.CompletionTokens);
            
            // 处理响应...
            var toolCalls = response.Message.Contents.OfType<FunctionCallContent>().ToList();
            
            if (toolCalls.Count == 0)
            {
                // 没有工具调用，返回最终结果
                return AgentResult.Success(output, context, totalTokenUsage, iteration + 1);
            }
            
            // 执行工具调用
            await HandleToolCallsAsync(context, toolCalls, variables, cancellationToken);
        }
        
        return AgentResult.Failure("MAX_ITERATIONS", "Reached maximum iterations");
    }
}
```

**特点**：
- 实现标准的工具调用循环
- 支持可配置的最大迭代次数
- 自动管理上下文 Token 限制
- 支持子 Agent 调用

### 2.2 SubAgentTool

将 Agent 包装为 Tool，实现多 Agent 协作的核心能力。

```csharp
/// <summary>
/// 子 Agent 工具 - 将 Agent 包装为可调用的 Tool
/// 继承自 ToolBase<SubAgentTool.Parameters>
/// </summary>
public class SubAgentTool : ToolBase<SubAgentTool.Parameters>
{
    private readonly IAgent _agent;
    private readonly ModelProvider _modelProvider;
    
    public override string Name { get; }
    public override string Summary => $"Delegate task to {_agent.Name}";
    public override string Description { get; }
    public override string Category => "Agent Delegation";
    
    public SubAgentTool(IAgent agent, ModelProvider modelProvider)
    {
        _agent = agent;
        _modelProvider = modelProvider;
        Name = $"delegate_to_{agent.Id}";
        Description = BuildDescription();
    }
    
    protected override async Task<ToolResult> ExecuteAsync(
        Parameters parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 1. 创建子 Agent 上下文
        var childContext = DefaultContextManager.StartNew(
            new SimpleTokenEstimator(),
            new ContextManagerOptions());
        
        // 2. 设置系统提示
        childContext.SetSystemMessage(_agent.Options.SystemPrompt ?? "");
        
        // 3. 添加父上下文摘要（如果有）
        if (context.ParentContext != null)
        {
            var summary = context.ParentContext.GenerateSummary();
            childContext.AddUserMessage(BuildInput(parameters.Task, summary));
        }
        else
        {
            childContext.AddUserMessage(parameters.Task);
        }
        
        // 4. 执行子 Agent
        var result = await _agent.ExecuteAsync(childContext, context.Variables, cancellationToken);
        
        if (result.IsSuccess)
        {
            return ToolResult.Success(result.Output ?? "Agent completed without output");
        }
        else
        {
            return ToolResult.Failure(
                $"Agent failed: {result.Error?.Message}",
                result.Error?.Code,
                result.IsRetryable);
        }
    }
    
    public class Parameters
    {
        [Required]
        [Description("The task to delegate to the agent")]
        public string Task { get; set; } = "";
    }
}
```

**使用示例**：

```csharp
// 使用 AgentBuilder 创建带子 Agent 的协调器
var coordinatorAgent = AgentBuilder.Create("coordinator")
    .WithName("Coordinator Agent")
    .WithDescription("Coordinates multiple specialist agents")
    .WithSystemPrompt(coordinatorPrompt)
    .WithSubAgent(logAnalysisAgent)      // 自动包装为 SubAgentTool
    .WithSubAgent(metricsAnalysisAgent)
    .WithSubAgent(playbookAgent)
    .Build(modelProvider);
```

**使用场景**：
- 协调者 Agent 调度专家 Agent
- 复杂任务的分解和委托
- Agent 间的上下文传递（通过摘要）

---

## 3. Business 层：SRE Agent 实现

### 3.1 业务 Agent 设计原则

1. **使用 AgentBuilder 流畅 API**：通过 `AgentBuilder` 创建 Agent
2. **专注业务逻辑**：System Prompt、工具选择、特定行为
3. **可配置性**：通过选项支持定制

### 3.2 创建业务 Agent 的方式

#### 方式一：静态工厂方法（当前实现）

当前项目使用静态工厂方法创建 Agent：

```csharp
/// <summary>
/// SRE 协调器 Agent - 负责故障分析和任务管理
/// </summary>
public static class SreCoordinatorAgent
{
    public const string AgentId = "sre-coordinator";
    public const string AgentName = "SRE 故障分析协调器";
    public const string AgentDescription = "负责分析线上故障告警，制定故障分析和排查计划";
    
    private const string SystemPrompt = """
        你是一个专业的SRE故障分析协调器。你的职责是：
        1. 接收和分析线上故障告警
        2. 识别故障类型和影响范围
        3. 制定故障分析和排查计划
        4. 使用 todo 工具管理分析任务
        
        ## 故障分析框架
        
        ### 故障识别
        - 告警类型（性能/可用性/安全/资源）
        - 影响范围（服务/用户/业务）
        - 紧急程度（P0-P3）
        
        ### 初步诊断方向
        - 基础设施层（网络/存储/计算）
        - 应用层（服务/依赖/配置）
        - 数据层（数据库/缓存/队列）
        - 外部依赖（第三方服务/API）
        
        ### 分析计划制定
        使用 todo_write 工具创建结构化的分析计划，包括：
        - 信息收集任务
        - 日志分析任务
        - 指标检查任务
        - 根因定位任务
        """;
    
    /// <summary>
    /// 创建 SRE 协调器 Agent
    /// </summary>
    public static ToolLoopAgent Create(
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
            .WithTool(new TodoWriteTool(todoService))
            .WithTool(new TodoReadTool(todoService))
            .WithLogger(logger)
            .Build(modelProvider);
    }
}
```

#### 方式二：工厂类模式（扩展方向）

```csharp
/// <summary>
/// SRE Agent 工厂 - 创建具体的业务 Agent
/// </summary>
public class SreAgentFactory
{
    private readonly ModelProvider _modelProvider;
    private readonly IServiceProvider _serviceProvider;
    
    public IAgent CreateDiagnosticAgent(DiagnosticAgentOptions options)
    {
        return AgentBuilder.Create("diagnostic")
            .WithName("Diagnostic Agent")
            .WithDescription("Comprehensive diagnosis and root cause analysis agent")
            .WithSystemPrompt(LoadPrompt("diagnostic_agent.md"))
            .WithModelCapability(options.ModelCapability ?? ModelCapability.Large)
            .WithMaxIterations(options.MaxIterations ?? 15)
            .WithTemperature(0.3)
            .WithTools(GetDiagnosticTools())
            .Build(_modelProvider);
    }
    
    public IAgent CreateLogAnalysisAgent(LogAnalysisAgentOptions options)
    {
        return AgentBuilder.Create("log_analysis")
            .WithName("Log Analysis Agent")
            .WithDescription("Specialized agent for log querying and analysis")
            .WithSystemPrompt(LoadPrompt("log_analysis_agent.md"))
            .WithModelCapability(ModelCapability.Medium)  // 日志分析用中等模型
            .WithMaxIterations(options.MaxIterations ?? 5)
            .WithTemperature(0.1)  // 更低的温度提高确定性
            .WithTools(GetLogAnalysisTools())
            .Build(_modelProvider);
    }
    
    // ... 其他 Agent 创建方法
}
```

### 3.3 SRE 业务 Agent 定义

#### 3.3.1 当前已实现

| Agent | 职责 | 使用的工具 | 模型能力 |
|-------|------|-----------|----------|
| SreCoordinatorAgent | 故障分析协调、任务管理 | TodoRead, TodoWrite | Medium |

#### 3.3.2 规划中的 Agent

| Agent | 职责 | 使用的工具 | 模型能力 |
|-------|------|-----------|----------|
| DiagnosticAgent | 综合诊断和根因分析 | All | Large |
| LogAnalysisAgent | 日志查询和分析 | SumoLogic, CloudWatch Logs | Medium |
| MetricsAnalysisAgent | 指标查询和分析 | Prometheus, CloudWatch Metrics | Medium |
| PlaybookAgent | 检索和匹配 Playbook | Knowledge Base | Small |
| K8sAgent | K8S 资源检查 | K8S API | Medium |
| AWSAgent | AWS 资源检查 | AWS SDK | Medium |

---

## 4. 多 Agent 协作模式

### 4.1 协调者模式

```
                    ┌─────────────────┐
                    │   Coordinator   │
                    │     Agent       │
                    └────────┬────────┘
                             │ 使用 SubAgentTool 调用
           ┌─────────────────┼─────────────────┐
           │                 │                 │
           ▼                 ▼                 ▼
    ┌────────────┐    ┌────────────┐    ┌────────────┐
    │    Log     │    │  Metrics   │    │  Playbook  │
    │   Agent    │    │   Agent    │    │   Agent    │
    └────────────┘    └────────────┘    └────────────┘
```

```csharp
/// <summary>
/// 协调者 Agent 示例 - 使用 AgentBuilder
/// </summary>
public static class CoordinatorAgentFactory
{
    public static IAgent Create(ModelProvider modelProvider, IServiceProvider services)
    {
        // 创建专家 Agent
        var logAgent = CreateLogAnalysisAgent(modelProvider);
        var metricsAgent = CreateMetricsAnalysisAgent(modelProvider);
        var playbookAgent = CreatePlaybookAgent(modelProvider);
        
        // 使用 AgentBuilder 创建协调者
        // WithSubAgent 会自动将 Agent 包装为 SubAgentTool
        return AgentBuilder.Create("coordinator")
            .WithName("Coordinator Agent")
            .WithDescription("Coordinates diagnosis by delegating to specialist agents")
            .WithSystemPrompt(GetCoordinatorPrompt())
            .WithModelCapability(ModelCapability.Large)
            .WithMaxIterations(10)
            .WithSubAgent(logAgent)       // 自动包装为 delegate_to_log_analysis
            .WithSubAgent(metricsAgent)   // 自动包装为 delegate_to_metrics_analysis
            .WithSubAgent(playbookAgent)  // 自动包装为 delegate_to_playbook
            .Build(modelProvider);
    }
    
    private static string GetCoordinatorPrompt() => """
        你是一个 SRE 诊断协调者。你的职责是：
        1. 分析告警，确定需要哪些专家来协助诊断
        2. 将任务分配给合适的专家 Agent
        3. 综合各专家的发现，得出最终诊断结论
        
        可用的专家工具：
        - delegate_to_log_analysis: 日志分析专家
        - delegate_to_metrics_analysis: 指标分析专家
        - delegate_to_playbook: Playbook 检索专家
        
        工作流程：
        1. 首先理解告警内容
        2. 并行调用相关专家收集信息
        3. 综合分析各方面信息
        4. 如需更多信息，可再次调用专家
        5. 得出诊断结论和建议
        """;
}
```

### 4.2 管道模式

```
┌─────────┐     ┌─────────┐     ┌─────────┐     ┌─────────┐
│ Triage  │ ──► │  Log    │ ──► │ Metrics │ ──► │ Report  │
│  Agent  │     │  Agent  │     │  Agent  │     │  Agent  │
└─────────┘     └─────────┘     └─────────┘     └─────────┘
```

```csharp
/// <summary>
/// 管道式执行多个 Agent
/// </summary>
public class AgentPipeline
{
    private readonly List<IAgent> _agents;
    
    public AgentPipeline(IEnumerable<IAgent> agents)
    {
        _agents = agents.ToList();
    }
    
    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext initialContext,
        CancellationToken cancellationToken = default)
    {
        var currentContext = initialContext;
        AgentResult? lastResult = null;
        var allMessages = new List<Message>();
        
        foreach (var agent in _agents)
        {
            lastResult = await agent.ExecuteAsync(currentContext, cancellationToken);
            allMessages.AddRange(lastResult.Messages);
            
            if (!lastResult.IsSuccess)
            {
                // 管道中断，返回错误
                return lastResult with { Messages = allMessages };
            }
            
            // 将上一个 Agent 的输出作为下一个的输入
            currentContext = currentContext with
            {
                Input = lastResult.Output ?? "",
                Variables = new Dictionary<string, object>(currentContext.Variables)
                {
                    [$"{agent.Id}_result"] = lastResult.Output ?? ""
                }
            };
        }
        
        return lastResult ?? AgentResult.Failure(
            new AgentError("EMPTY_PIPELINE", "No agents in pipeline"),
            allMessages);
    }
}
```

### 4.3 文件共享上下文

Agent 之间可通过文件系统共享上下文，实现更复杂的协作。

```
/workspaces/sessions/{session-id}/
├── context.json              # 共享上下文
├── findings/                 # 各 Agent 的发现
│   ├── log_analysis.json
│   ├── metrics_analysis.json
│   └── playbook.json
├── decisions/                # 决策记录
│   └── diagnosis.json
└── artifacts/                # 产出物
    └── report.md
```

```csharp
/// <summary>
/// 会话工作空间管理
/// </summary>
public interface ISessionWorkspace
{
    Task<string> GetWorkspacePathAsync(Guid sessionId);
    Task WriteContextAsync(Guid sessionId, object context);
    Task<T?> ReadContextAsync<T>(Guid sessionId);
    Task WriteFindingAsync(Guid sessionId, string agentId, object finding);
    Task<T?> ReadFindingAsync<T>(Guid sessionId, string agentId);
}

/// <summary>
/// 带工作空间的 Agent 包装
/// </summary>
public class WorkspaceAwareAgent : IAgent
{
    private readonly IAgent _innerAgent;
    private readonly ISessionWorkspace _workspace;
    
    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // 执行前加载共享上下文
        var sharedContext = await _workspace.ReadContextAsync<SharedContext>(context.SessionId);
        var enhancedContext = EnhanceWithSharedContext(context, sharedContext);
        
        // 执行 Agent
        var result = await _innerAgent.ExecuteAsync(enhancedContext, cancellationToken);
        
        // 执行后保存发现
        if (result.IsSuccess)
        {
            await _workspace.WriteFindingAsync(
                context.SessionId, 
                Id, 
                ExtractFinding(result));
        }
        
        return result;
    }
}
```

---

## 5. Hooks 在多 Agent 中的应用

### 5.1 动态模型选择

```csharp
public class DynamicModelHooks : DefaultAgentHooks
{
    public override async Task<HookResult> OnBeforeLLMCallAsync(BeforeLLMCallContext context)
    {
        // 根据任务复杂度选择模型
        var complexity = EstimateComplexity(context.Messages);
        
        context.Model = complexity switch
        {
            Complexity.Simple => "gpt-4o-mini",
            Complexity.Medium => "gpt-4o",
            Complexity.Complex => "gpt-4",
            _ => context.Model
        };
        
        return HookResult.Continue();
    }
    
    private Complexity EstimateComplexity(List<Message> messages)
    {
        // 根据消息数量、工具调用次数等估算复杂度
        var toolCallCount = messages
            .SelectMany(m => m.Parts.OfType<ToolCallPart>())
            .Count();
            
        if (toolCallCount > 10) return Complexity.Complex;
        if (toolCallCount > 5) return Complexity.Medium;
        return Complexity.Simple;
    }
}
```

### 5.2 Agent 间通信记录

```csharp
public class AgentCommunicationHooks : DefaultAgentHooks
{
    private readonly ISessionWorkspace _workspace;
    
    public override async Task OnAfterExecuteAsync(AfterExecuteContext context)
    {
        // 记录 Agent 执行结果到共享工作空间
        await _workspace.WriteFindingAsync(
            context.SessionId,
            context.AgentId,
            new AgentFinding
            {
                AgentId = context.AgentId,
                Output = context.Result.Output,
                IsSuccess = context.Result.IsSuccess,
                TokenUsage = context.Result.TokenUsage,
                CompletedAt = DateTime.UtcNow
            });
    }
}
```

### 5.3 审批控制

```csharp
public class ApprovalHooks : DefaultAgentHooks
{
    private readonly IApprovalService _approvalService;
    
    public override async Task<HookResult> OnBeforeToolCallAsync(BeforeToolCallContext context)
    {
        // 检查工具是否需要审批
        var requiresApproval = await _approvalService.CheckRequiresApprovalAsync(
            context.ToolName,
            context.Parameters);
        
        if (requiresApproval)
        {
            // 请求审批
            var approved = await _approvalService.RequestApprovalAsync(
                context.SessionId,
                context.ToolName,
                context.Parameters);
            
            if (!approved)
            {
                return HookResult.Abort($"Tool {context.ToolName} was not approved");
            }
        }
        
        return HookResult.Continue();
    }
}
```

---

## 6. Agent 配置与注册

### 6.1 配置文件

```yaml
sre_agents:
  coordinator:
    enabled: true
    model: "gpt-4"
    max_iterations: 10
    system_prompt_file: "prompts/coordinator.md"
    delegate_agents:
      - log_analysis
      - metrics_analysis
      - playbook
      
  log_analysis:
    enabled: true
    model: "gpt-4o-mini"
    max_iterations: 5
    temperature: 0.1
    system_prompt_file: "prompts/log_analysis.md"
    tools:
      - sumo_logic_query
      - cloudwatch_logs_query
      
  metrics_analysis:
    enabled: true
    model: "gpt-4o-mini"
    max_iterations: 5
    temperature: 0.1
    system_prompt_file: "prompts/metrics_analysis.md"
    tools:
      - prometheus_query
      - cloudwatch_metrics_query
      
  playbook:
    enabled: true
    model: "gpt-4o-mini"
    max_iterations: 3
    system_prompt_file: "prompts/playbook.md"
    tools:
      - knowledge_base_search
```

### 6.2 依赖注入注册

```csharp
public static class SreAgentServiceExtensions
{
    public static IServiceCollection AddSreAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册 Agent 工厂
        services.AddSingleton<SreAgentFactory>();
        
        // 注册 Hooks
        services.AddSingleton<IAgentHooks, SreAgentHooks>();
        
        // 根据配置注册各个 Agent
        var config = configuration.GetSection("sre_agents");
        
        services.AddSingleton<IAgent>(sp =>
        {
            var factory = sp.GetRequiredService<SreAgentFactory>();
            return factory.CreateCoordinatorAgent(
                config.GetSection("coordinator").Get<CoordinatorAgentOptions>()!);
        });
        
        return services;
    }
}
```

---

## 7. 监控与可观测性

### 7.1 Agent 执行指标

通过 Hooks 收集指标：

```csharp
public class MetricsHooks : DefaultAgentHooks
{
    private readonly AgentMetrics _metrics;
    
    public override async Task OnAfterLLMCallAsync(AfterLLMCallContext context)
    {
        _metrics.RecordLLMCall(
            context.AgentId,
            context.Model,
            context.TokenUsage,
            context.Duration);
    }
    
    public override async Task OnAfterToolCallAsync(AfterToolCallContext context)
    {
        _metrics.RecordToolCall(
            context.AgentId,
            context.ToolName,
            context.Result.IsSuccess,
            context.Duration);
    }
    
    public override async Task OnAfterExecuteAsync(AfterExecuteContext context)
    {
        _metrics.RecordAgentExecution(
            context.AgentId,
            context.Result.IsSuccess,
            context.TotalDuration);
    }
}
```

### 7.2 分布式追踪

```csharp
public class TracingHooks : DefaultAgentHooks
{
    private static readonly ActivitySource ActivitySource = new("SreAgent");
    
    public override async Task<HookResult> OnBeforeExecuteAsync(BeforeExecuteContext context)
    {
        var activity = ActivitySource.StartActivity($"Agent.{context.AgentId}");
        activity?.SetTag("session.id", context.SessionId.ToString());
        activity?.SetTag("agent.id", context.AgentId);
        
        context.Options.Metadata["activity"] = activity;
        
        return HookResult.Continue();
    }
    
    public override async Task OnAfterExecuteAsync(AfterExecuteContext context)
    {
        if (context.Result.Metadata.TryGetValue("activity", out var activityObj) 
            && activityObj is Activity activity)
        {
            activity.SetTag("result.success", context.Result.IsSuccess);
            activity.Dispose();
        }
    }
}
```
