# SRE Agent 项目文档

本文档目录包含 SRE Agent 项目的详细设计和实施文档。

## 文档索引

| 序号 | 文档 | 说明 |
|------|------|------|
| 01 | [项目概述与目标](./01-项目概述与目标.md) | 项目背景、目标、成功指标和风险分析 |
| 02 | [团队现状与基础设施](./02-团队现状与基础设施.md) | 团队技术栈、工具链和基础设施现状 |
| 03 | [技术架构设计](./03-技术架构设计.md) | **核心文档**：Framework 与业务分离、Hooks 系统、Result 模式、数据模型 |
| 04 | [多Agent架构详解](./04-多Agent架构详解.md) | 多 Agent 协作、AgentAsTool、业务 Agent 实现 |
| 05 | [上下文管理策略](./05-上下文管理策略.md) | Session-Message-Part 模型、上下文裁剪、Skill 加载 |
| 06 | [工具系统设计](./06-工具系统设计.md) | Result 模式详解、Tool 实现示例、错误处理 |
| 07 | [执行过程持久化](./07-执行过程持久化.md) | 会话管理、断点恢复和人工干预 |
| 08 | [事件总线与可观测性](./08-事件总线与可观测性.md) | 事件系统、SSE 输出、监控指标 |
| 09 | [Slack集成与告警接入](./09-Slack集成与告警接入.md) | Slack App 配置、告警解析和交互 |
| 10 | [知识库与RAG实现](./10-知识库与RAG实现.md) | 知识库架构、Confluence 同步和 RAG |
| 11 | [项目实施计划](./11-项目实施计划.md) | 分期计划、里程碑和资源需求 |

## 核心设计原则

### 1. Framework 与业务分离

```
┌─────────────────────────────────┐
│     Application Layer           │  ← 业务 Agent、业务 Tool
│     (SRE Agent 具体实现)         │
└───────────────┬─────────────────┘
                │ 依赖
                ▼
┌─────────────────────────────────┐
│      Agent Framework            │  ← 通用抽象、基础实现、Hooks
│      (通用框架)                  │
└─────────────────────────────────┘
```

- **Agent Framework** 只提供抽象接口和基础实现（`ToolLoopAgent`、`AgentAsTool`）
- **Business Layer** 实现具体的 SRE Agent 和工具
- Framework 不包含任何业务逻辑

### 2. Result 模式

所有工具调用返回 `ToolResult`，不抛异常：

```csharp
// ✅ 正确做法
return ToolResult.Failure(
    "Query syntax error: missing closing parenthesis",
    errorCode: "SYNTAX_ERROR",
    isRetryable: true);

// ❌ 错误做法
throw new Exception("Query failed");
```

### 3. Hooks 系统

允许业务层在 Agent 执行各阶段介入：

```csharp
public interface IAgentHooks
{
    Task<HookResult> OnBeforeLLMCallAsync(BeforeLLMCallContext context);  // 可修改模型、参数
    Task<HookResult> OnBeforeToolCallAsync(BeforeToolCallContext context); // 可控制审批
    Task<HookResult> OnIterationEndAsync(IterationEndContext context);     // 可提前终止
    // ...
}
```

### 4. 三层数据模型

```
Session → Message → Part
```

- **Session**: 一次完整的会话/任务
- **Message**: 对话中的一条消息（System/User/Assistant/Tool）
- **Part**: 消息的组成部分（Text/ToolCall/ToolResult/Image/...）

### 5. 轻量级通信

- 使用 **SSE (Server-Sent Events)** 替代 SignalR
- Framework 层不引入重量级通信框架
- 业务层可根据需要扩展

## 快速导航

### 按角色阅读

**技术负责人/架构师**
1. [项目概述与目标](./01-项目概述与目标.md)
2. [技术架构设计](./03-技术架构设计.md) ⭐ 核心
3. [项目实施计划](./11-项目实施计划.md)

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
    Task<AgentResult> ExecuteAsync(AgentExecutionContext context, CancellationToken ct);
}

// Tool 接口
public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement ParameterSchema { get; }
    Task<ToolResult> ExecuteAsync(ToolExecutionContext context, CancellationToken ct);
}

// 上下文管理器接口
public interface IContextManager
{
    Task AddMessageAsync(Message message, CancellationToken ct);
    Task<IReadOnlyList<Message>> GetMessagesForCompletionAsync(int maxTokens, CancellationToken ct);
    int EstimatedTokenCount { get; }
}

// Hooks 接口
public interface IAgentHooks
{
    Task<HookResult> OnBeforeExecuteAsync(BeforeExecuteContext context);
    Task<HookResult> OnBeforeLLMCallAsync(BeforeLLMCallContext context);
    Task<HookResult> OnBeforeToolCallAsync(BeforeToolCallContext context);
    Task<HookResult> OnIterationEndAsync(IterationEndContext context);
    // ...
}
```

## 文档更新记录

| 日期 | 版本 | 更新内容 |
|------|------|----------|
| 2026-01-23 | v1.1 | 重构架构：Framework 与业务分离、Hooks 系统、Result 模式、三层数据模型 |
| 2026-01-23 | v1.0 | 初始文档创建 |
