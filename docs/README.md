# SRE Agent 项目文档

本文档目录包含 SRE Agent 项目的详细设计和实施文档。

## 项目结构

```
src/
├── SreAgent.Api/              # Web API 层
├── SreAgent.Application/      # 应用层（业务 Agent 和工具）
├── SreAgent.Framework/        # 通用 Agent 框架
├── SreAgent.Infrastructure/   # 基础设施层（待实现）
└── SreAgent.Repository/       # 数据访问层（待实现）
```

## 文档索引

| 序号 | 文档 | 说明 |
|------|------|------|
| 01 | [项目概述与目标](./01-项目概述与目标.md) | 项目背景、目标、成功指标和风险分析 |
| 02 | [团队现状与基础设施](./02-团队现状与基础设施.md) | 团队技术栈、工具链和基础设施现状 |
| 03 | [技术架构设计](./03-技术架构设计.md) | **核心文档**：项目结构、AgentBuilder、ModelProvider、Result 模式 |
| 04 | [多Agent架构详解](./04-多Agent架构详解.md) | 多 Agent 协作、SubAgentTool、SreCoordinatorAgent |
| 05 | [上下文管理策略](./05-上下文管理策略.md) | 充血模型 ContextManager、剪枝策略、Token 管理 |
| 06 | [工具系统设计](./06-工具系统设计.md) | ToolBase 泛型基类、Todo 工具实现、错误处理 |
| 07 | [执行过程持久化](./07-执行过程持久化.md) | 会话管理、断点恢复和人工干预 |
| 08 | [事件总线与可观测性](./08-事件总线与可观测性.md) | 事件系统、SSE 输出、监控指标 |
| 09 | [Slack集成与告警接入](./09-Slack集成与告警接入.md) | Slack App 配置、告警解析和交互 |
| 10 | [知识库与RAG实现](./10-知识库与RAG实现.md) | 知识库架构、Confluence 同步和 RAG |
| 11 | [项目实施计划](./11-项目实施计划.md) | 分期计划、里程碑和资源需求 |
| 12 | [功能实现状态与长期开发计划](./12-功能实现状态与长期开发计划.md) | **长期文档**：当前实现总结、建议开发路线图 |

## 核心设计原则

### 1. Framework 与业务分离

```
┌─────────────────────────────────┐
│     Application Layer           │  ← 业务 Agent、业务 Tool
│  (SreCoordinatorAgent, TodoTool)│
└───────────────┬─────────────────┘
                │ 依赖
                ▼
┌─────────────────────────────────┐
│      Agent Framework            │  ← 通用抽象、基础实现
│   (ToolLoopAgent, ToolBase)     │
└─────────────────────────────────┘
```

- **Agent Framework** 提供 `ToolLoopAgent`、`ToolBase<TParams>`、`SubAgentTool` 等基础能力
- **Business Layer** 实现 `SreCoordinatorAgent`、`TodoWriteTool` 等具体业务
- Framework 不包含任何业务逻辑

### 2. AgentBuilder 流畅 API

使用流畅 API 创建 Agent：

```csharp
var agent = AgentBuilder.Create("sre-coordinator")
    .WithName("SRE 故障分析协调器")
    .WithSystemPrompt(systemPrompt)
    .WithModelCapability(ModelCapability.Medium)
    .WithMaxIterations(15)
    .WithTemperature(0.3)
    .WithTool(new TodoWriteTool(todoService))
    .Build(modelProvider);
```

### 3. 全局 Result 模式

**整个系统**统一使用 Result 模式：

| Result 类型 | 应用场景 |
|------------|---------|
| `AgentResult` | Agent 执行结果 |
| `ToolResult` | 工具执行结果 |
| `TokenUsage` | Token 使用统计 |

```csharp
// ✅ 正确做法 - Tool
return ToolResult.Failure("Query syntax error", "SYNTAX_ERROR", isRetryable: true);

// ✅ 正确做法 - Agent
return AgentResult.Success(output, context, tokenUsage, iterationCount);

// ❌ 错误做法
throw new Exception("Failed");
```

### 4. 充血模型 ContextManager

上下文管理器采用充血模型，封装完整的管理逻辑：

```csharp
// 创建上下文
var context = DefaultContextManager.StartNew(new SimpleTokenEstimator());

// 语义化添加消息
context.SetSystemMessage(systemPrompt);
context.AddUserMessage(userInput);
context.AddToolResultMessage(toolCallId, toolName, result);

// 获取 ChatMessage 用于 LLM 调用（自动处理剪枝）
var messages = context.GetChatMessages();
```

### 5. 泛型工具基类

使用强类型参数定义工具：

```csharp
public class TodoWriteTool : ToolBase<TodoWriteParams>
{
    protected override async Task<ToolResult> ExecuteAsync(
        TodoWriteParams parameters,  // 强类型参数
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        // 业务逻辑
    }
}
```

## 快速导航

### 按角色阅读

**技术负责人/架构师**
1. [项目概述与目标](./01-项目概述与目标.md)
2. [技术架构设计](./03-技术架构设计.md) ⭐ 核心
3. [功能实现状态与长期开发计划](./12-功能实现状态与长期开发计划.md) ⭐ 长期路线图
4. [项目实施计划](./11-项目实施计划.md)

**Agent Framework 开发**
1. [技术架构设计](./03-技术架构设计.md) - 核心抽象、Hooks 系统
2. [上下文管理策略](./05-上下文管理策略.md) - IContextManager 接口
3. [工具系统设计](./06-工具系统设计.md) - ITool 接口、Result 模式

**业务 Agent 开发**
1. [多Agent架构详解](./04-多Agent架构详解.md) - 如何实现业务 Agent
2. [工具系统设计](./06-工具系统设计.md) - 如何实现业务工具
3. [知识库与RAG实现](./10-知识库与RAG实现.md) - RAG 集成

**集成开发**
1. [团队现状与基础设施](./02-团队现状与基础设施.md)
2. [Slack集成与告警接入](./09-Slack集成与告警接入.md)
3. [事件总线与可观测性](./08-事件总线与可观测性.md)

## 技术栈概览

| 类别 | 技术选型 |
|------|----------|
| 运行时 | .NET 10 + C# |
| LLM | OpenAI API 兼容接口 |
| AI 封装 | Microsoft.Extensions.AI |
| 向量存储 | AWS Knowledge Base |
| 数据库 | PostgreSQL + EF Core |
| 实时通信 | SSE (Server-Sent Events) |
| 消息通知 | Slack |

## 关键接口速查

```csharp
// Agent 接口
public interface IAgent
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    AgentOptions Options { get; }
    Task<AgentResult> ExecuteAsync(
        IContextManager context,
        IReadOnlyDictionary<string, object>? variables = null,
        CancellationToken cancellationToken = default);
}

// Tool 接口
public interface ITool
{
    string Name { get; }
    string Summary { get; }
    string Description { get; }
    string Category { get; }
    ToolDetail GetDetail();
    Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct);
}

// 泛型工具基类
public abstract class ToolBase<TParams> : ITool where TParams : class, new()
{
    protected abstract Task<ToolResult> ExecuteAsync(
        TParams parameters,
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}

// 上下文管理器接口（充血模型）
public interface IContextManager
{
    Guid SessionId { get; }
    int EstimatedTokenCount { get; }
    void ConfigureTokenLimit(ModelTokenLimits tokenLimits);
    void SetSystemMessage(string content);
    void AddUserMessage(string content);
    void AddToolResultMessage(string toolCallId, string toolName, ToolResult result);
    IReadOnlyList<ChatMessage> GetChatMessages();
    ContextSnapshot ExportSnapshot(Dictionary<string, object>? metadata = null);
}

// Agent 结果
public record AgentResult
{
    public bool IsSuccess { get; init; }
    public string? Output { get; init; }
    public AgentError? Error { get; init; }
    public IContextManager? Context { get; init; }
    public TokenUsage TokenUsage { get; init; }
    public int IterationCount { get; init; }
    public bool IsRetryable { get; init; }
}

// 工具结果
public record ToolResult
{
    public bool IsSuccess { get; init; }
    public string Content { get; init; }
    public object? Data { get; init; }
    public string? ErrorCode { get; init; }
    public bool IsRetryable { get; init; }
    public TimeSpan Duration { get; init; }
}
```

## 当前实现状态

### 已实现

| 模块 | 状态 | 说明 |
|------|------|------|
| ToolLoopAgent | ✅ | 核心 Agent 执行引擎 |
| AgentBuilder | ✅ | 流畅 API 构建 Agent |
| ToolBase<TParams> | ✅ | 泛型工具基类 |
| DefaultContextManager | ✅ | 充血模型上下文管理 |
| ModelProvider | ✅ | 模型提供者管理（阿里云百炼、智谱） |
| SubAgentTool | ✅ | 子 Agent 包装工具 |
| SreCoordinatorAgent | ✅ | SRE 协调器 Agent |
| TodoWriteTool/TodoReadTool | ✅ | 任务管理工具 |
| API 层 | ✅ | Chat 和 Analyze 端点 |

### 待实现

| 模块 | 状态 | 说明 |
|------|------|------|
| 日志分析工具 | ✅ 部分 | CloudWatch Logs 已实现，SumoLogic 待开发 |
| 指标查询工具 | ⏳ | Prometheus、CloudWatch Metrics |
| K8S 工具 | ⏳ | Kubernetes 资源查询 |
| AWS 工具 | ⏳ | AWS 资源管理 |
| 知识库集成 | ⏳ | AWS Knowledge Base、RAG |
| Slack 集成 | ⏳ | 告警接入、交互 |
| 持久化层 | ⏳ | PostgreSQL、会话存储 |
| Hooks 系统 | ⏳ | 业务层介入钩子 |

## 文档更新记录

| 日期 | 版本 | 更新内容 |
|------|------|----------|
| 2026-01-28 | v1.2 | 同步文档与实际代码实现：AgentBuilder、ToolBase 泛型、充血模型 ContextManager |
| 2026-01-23 | v1.1 | 重构架构：Framework 与业务分离、Hooks 系统、Result 模式、三层数据模型 |
| 2026-01-23 | v1.0 | 初始文档创建 |
