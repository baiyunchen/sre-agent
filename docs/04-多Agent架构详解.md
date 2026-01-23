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
// Framework 层提供的基础实现
public class ToolLoopAgent : IAgent
{
    // 标准的 Think -> Act -> Observe 循环
    // 不包含任何业务逻辑
}
```

**特点**：
- 实现标准的工具调用循环
- 支持可配置的最大迭代次数
- 集成 Hooks 系统
- 支持上下文管理

### 2.2 AgentAsTool

将 Agent 包装为 Tool，实现多 Agent 协作的核心能力。

```csharp
// 将任意 Agent 包装为 Tool
var diagnosticTool = new AgentAsTool(diagnosticAgent, new AgentAsToolOptions
{
    ToolName = "analyze_with_diagnostic_agent",
    ToolDescription = "Delegate complex diagnostic tasks to the diagnostic specialist agent"
});

// 然后可以被其他 Agent 作为工具调用
var coordinatorAgent = new ToolLoopAgent(
    id: "coordinator",
    name: "Coordinator Agent",
    description: "Coordinates multiple specialist agents",
    chatClient: chatClient,
    options: new AgentOptions
    {
        Tools = new[] { diagnosticTool, logAnalysisTool, metricsTool }
    }
);
```

**使用场景**：
- 协调者 Agent 调度专家 Agent
- 复杂任务的分解和委托
- Agent 间的信息传递

---

## 3. Business 层：SRE Agent 实现

### 3.1 业务 Agent 设计原则

1. **继承或组合 Framework 能力**：基于 `ToolLoopAgent` 构建
2. **专注业务逻辑**：System Prompt、工具选择、特定行为
3. **可配置性**：通过选项和 Hooks 支持定制

### 3.2 创建业务 Agent 的方式

#### 方式一：工厂模式（推荐）

```csharp
/// <summary>
/// SRE Agent 工厂 - 创建具体的业务 Agent
/// </summary>
public class SreAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    
    public IAgent CreateDiagnosticAgent(DiagnosticAgentOptions options)
    {
        var tools = new List<ITool>
        {
            _serviceProvider.GetRequiredService<SumoLogicQueryTool>(),
            _serviceProvider.GetRequiredService<PrometheusQueryTool>(),
            _serviceProvider.GetRequiredService<CloudWatchLogsTool>(),
            _serviceProvider.GetRequiredService<K8sResourceTool>()
        };
        
        return new ToolLoopAgent(
            id: "diagnostic",
            name: "Diagnostic Agent",
            description: "Comprehensive diagnosis and root cause analysis agent",
            chatClient: _chatClient,
            options: new AgentOptions
            {
                Model = options.Model ?? "gpt-4",
                SystemPrompt = LoadPrompt("diagnostic_agent.md"),
                MaxIterations = options.MaxIterations ?? 15,
                Temperature = 0.3,
                Tools = tools
            },
            hooks: new SreAgentHooks(_serviceProvider)
        );
    }
    
    public IAgent CreateLogAnalysisAgent(LogAnalysisAgentOptions options)
    {
        var tools = new List<ITool>
        {
            _serviceProvider.GetRequiredService<SumoLogicQueryTool>(),
            _serviceProvider.GetRequiredService<CloudWatchLogsTool>()
        };
        
        return new ToolLoopAgent(
            id: "log_analysis",
            name: "Log Analysis Agent",
            description: "Specialized agent for log querying and analysis",
            chatClient: _chatClient,
            options: new AgentOptions
            {
                Model = options.Model ?? "gpt-4o-mini",  // 日志分析用更快的模型
                SystemPrompt = LoadPrompt("log_analysis_agent.md"),
                MaxIterations = options.MaxIterations ?? 5,
                Temperature = 0.1,  // 更低的温度提高确定性
                Tools = tools
            }
        );
    }
    
    // ... 其他 Agent 创建方法
}
```

#### 方式二：继承封装

```csharp
/// <summary>
/// 诊断 Agent - 通过继承封装业务逻辑
/// </summary>
public class DiagnosticAgent : IAgent
{
    private readonly ToolLoopAgent _innerAgent;
    private readonly IKnowledgeBaseClient _knowledgeBase;
    
    public string Id => "diagnostic";
    public string Name => "Diagnostic Agent";
    public string Description => "Comprehensive diagnosis and root cause analysis";
    
    public DiagnosticAgent(
        IChatClient chatClient,
        IEnumerable<ITool> tools,
        IKnowledgeBaseClient knowledgeBase,
        IAgentHooks? hooks = null)
    {
        _knowledgeBase = knowledgeBase;
        
        _innerAgent = new ToolLoopAgent(
            id: Id,
            name: Name,
            description: Description,
            chatClient: chatClient,
            options: new AgentOptions
            {
                SystemPrompt = GetSystemPrompt(),
                MaxIterations = 15,
                Temperature = 0.3,
                Tools = tools.ToList()
            },
            hooks: hooks
        );
    }
    
    public async Task<AgentResult> ExecuteAsync(
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        // 业务前置处理：检索相关 Playbook
        var ragContext = await RetrievePlaybookAsync(context);
        
        // 增强上下文
        var enhancedContext = context with
        {
            Variables = new Dictionary<string, object>(context.Variables)
            {
                ["playbook_context"] = ragContext
            }
        };
        
        // 委托给内部 Agent 执行
        return await _innerAgent.ExecuteAsync(enhancedContext, cancellationToken);
    }
    
    private async Task<string> RetrievePlaybookAsync(AgentExecutionContext context)
    {
        // RAG 检索逻辑
        var results = await _knowledgeBase.SearchAsync(context.Input);
        return FormatPlaybookContext(results);
    }
    
    private string GetSystemPrompt() => """
        你是一个专业的 SRE 诊断专家。你的职责是：
        1. 分析告警信息，理解问题症状
        2. 使用工具收集必要的诊断信息
        3. 结合 Playbook 知识进行根因分析
        4. 给出明确的诊断结论和建议
        
        诊断原则：
        - 先收集证据，再下结论
        - 优先检查最可能的原因
        - 记录每一步的推理过程
        ...
        """;
}
```

### 3.3 SRE 业务 Agent 定义

| Agent | 职责 | 使用的工具 | 模型建议 |
|-------|------|-----------|----------|
| DiagnosticAgent | 综合诊断和根因分析 | All | gpt-4 |
| LogAnalysisAgent | 日志查询和分析 | SumoLogic, CloudWatch Logs | gpt-4o-mini |
| MetricsAnalysisAgent | 指标查询和分析 | Prometheus, CloudWatch Metrics | gpt-4o-mini |
| PlaybookAgent | 检索和匹配 Playbook | Knowledge Base | gpt-4o-mini |
| K8sAgent | K8S 资源检查 | K8S API | gpt-4o-mini |
| AWSAgent | AWS 资源检查 | AWS SDK | gpt-4o-mini |
| CoordinatorAgent | 任务分解和协调 | AgentAsTools | gpt-4 |

---

## 4. 多 Agent 协作模式

### 4.1 协调者模式

```
                    ┌─────────────────┐
                    │   Coordinator   │
                    │     Agent       │
                    └────────┬────────┘
                             │ 使用 AgentAsTool 调用
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
/// 协调者 Agent 示例
/// </summary>
public class CoordinatorAgentFactory
{
    public IAgent Create(IServiceProvider services)
    {
        // 创建专家 Agent
        var logAgent = services.GetRequiredService<SreAgentFactory>()
            .CreateLogAnalysisAgent(new());
        var metricsAgent = services.GetRequiredService<SreAgentFactory>()
            .CreateMetricsAnalysisAgent(new());
        var playbookAgent = services.GetRequiredService<SreAgentFactory>()
            .CreatePlaybookAgent(new());
        
        // 将专家 Agent 包装为工具
        var tools = new List<ITool>
        {
            new AgentAsTool(logAgent, new AgentAsToolOptions
            {
                ToolName = "analyze_logs",
                ToolDescription = "Delegate log analysis to the log specialist. Use when you need to query and analyze application logs."
            }),
            new AgentAsTool(metricsAgent, new AgentAsToolOptions
            {
                ToolName = "analyze_metrics",
                ToolDescription = "Delegate metrics analysis to the metrics specialist. Use when you need to query and analyze system metrics."
            }),
            new AgentAsTool(playbookAgent, new AgentAsToolOptions
            {
                ToolName = "search_playbook",
                ToolDescription = "Search for relevant playbooks and troubleshooting guides."
            })
        };
        
        return new ToolLoopAgent(
            id: "coordinator",
            name: "Coordinator Agent",
            description: "Coordinates diagnosis by delegating to specialist agents",
            chatClient: services.GetRequiredService<IChatClient>(),
            options: new AgentOptions
            {
                SystemPrompt = GetCoordinatorPrompt(),
                MaxIterations = 10,
                Tools = tools
            }
        );
    }
    
    private string GetCoordinatorPrompt() => """
        你是一个 SRE 诊断协调者。你的职责是：
        1. 分析告警，确定需要哪些专家来协助诊断
        2. 将任务分配给合适的专家 Agent
        3. 综合各专家的发现，得出最终诊断结论
        
        可用的专家：
        - analyze_logs: 日志分析专家
        - analyze_metrics: 指标分析专家
        - search_playbook: Playbook 检索专家
        
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
