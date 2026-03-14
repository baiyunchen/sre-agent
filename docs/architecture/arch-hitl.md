# 架构设计: Human-in-the-Loop 完善

- Feature Key: `hitl`
- 版本: 1.0
- 更新日期: 2026-03-14

## 1. 概述

本设计实现三个 HITL 能力：实时观察与打断、中断后恢复、工具审批流程。技术栈与现有系统一致：.NET 9 / React 19 / SSE。

## 2. 架构决策

### 2.1 后台执行模型

- **决策**：analyze/chat 请求提交后立即返回（202 Accepted 或 200 + sessionId），Agent 在后台 `Task.Run` 中执行
- **理由**：避免 HTTP 长连接阻塞，支持 per-session SSE 实时推送
- **实现**：`SreController` / `SessionController` 启动 `Task.Run`，不 await；通过 `ISessionExecutionRegistry` 管理 per-session 的 `CancellationTokenSource` 与执行状态

### 2.2 Per-Session SSE

- **决策**：新增 `GET /api/sessions/{sessionId}/stream`，推送 session 级别执行事件
- **事件类型**：`agent.started`、`agent.completed`、`tool.started`、`tool.completed`、`session.ended`
- **实现**：通过 `ISessionStreamPublisher` 接口，`PersistenceExecutionTracker` 扩展或装饰器在 OnAgentStart/OnToolStart/OnToolComplete 时发布事件；SessionController 的 stream 端点订阅并写入 Response body

### 2.3 中断机制

- **决策**：维护 per-session `CancellationTokenSource` 字典，Interrupt API 调用 `Cancel()`
- **理由**：ToolLoopAgent 主循环已有 `ct.ThrowIfCancellationRequested()`，只需传入可取消的 token
- **实现**：`ISessionExecutionRegistry.Register(sessionId, cts)` 在启动时注册，`InterruptSessionAsync` 调用 `cts.Cancel()`；InterventionService 与 Registry 协作

### 2.4 恢复执行

- **决策**：Resume 时传递 `IExecutionTracker` 到 `agent.ExecuteAsync`，并在后台执行
- **实现**：`SessionRecoveryService.ResumeSessionAsync` 接收 `IExecutionTracker` 参数，构造 `variables` 传入 `ExecuteAsync`；Resume 端点启动后台 Task 后立即返回

### 2.5 工具审批

- **决策**：ToolExecutor 执行前检查 ApprovalRules；需要审批时创建 ToolInvocation（PendingApproval），session 置为 WaitingApproval，等待人类审批
- **实现**：`IApprovalRuleRepository` 查询规则；无 always-allow 且无 always-deny 时需审批；`ToolExecutor` 调用 `IApprovalWaiter` 等待审批完成；新增 per-tool approve/reject API

## 3. 组件设计

### 3.1 新增/修改接口

| 接口 | 职责 |
|------|------|
| `ISessionExecutionRegistry` | 注册/注销 per-session CancellationTokenSource，提供 GetToken(sessionId) |
| `ISessionStreamPublisher` | 发布 session 执行事件，供 SSE 端点订阅 |
| `IApprovalWaiter` | 等待工具审批完成（TaskCompletionSource 或轮询） |

### 3.2 数据流

```
[Analyze/Chat] -> Task.Run(ExecuteAsync) -> 立即返回 sessionId
                    |
                    v
            [ToolLoopAgent] --(IExecutionTracker)--> [PersistenceExecutionTracker]
                    |                                        |
                    |                                        +-> [ISessionStreamPublisher]
                    |
                    +-- 每次迭代 ct.ThrowIfCancellationRequested()
                    |
[Interrupt API] -> Registry.GetToken(sessionId).Cancel()
```

### 3.3 SSE 事件 Schema

```json
{
  "eventType": "tool.started",
  "sessionId": "uuid",
  "timestamp": "ISO8601",
  "payload": {
    "agentRunId": "uuid",
    "invocationId": "uuid",
    "toolName": "string",
    "parameters": "string"
  }
}
```

事件类型：`agent.started`、`agent.completed`、`tool.started`、`tool.completed`、`session.ended`

## 4. API 契约变更

### 4.1 新增端点

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/sessions/{sessionId}/stream` | Per-session SSE 流 |
| POST | `/api/sessions/{sessionId}/resume` | 已有，需改为 202 + 后台执行 |
| POST | `/api/sessions/{sessionId}/tool-invocations/{invocationId}/approve` | 工具级审批通过 |
| POST | `/api/sessions/{sessionId}/tool-invocations/{invocationId}/reject` | 工具级审批拒绝 |

### 4.2 修改端点

- `POST /api/sre/analyze`：可返回 202 Accepted + Location header（后台执行模式）
- `POST /api/sessions/{sessionId}/messages`：同上
- `POST /api/sessions/{sessionId}/resume`：改为 202，后台执行，通过 stream 观察

### 4.3 已有端点（无需契约变更）

- `POST /api/sessions/{sessionId}/interrupt`：已存在，需与 Registry 联动
- `POST /api/sessions/{sessionId}/cancel`：已存在

## 5. 技术风险与缓解

| 风险 | 缓解 |
|------|------|
| 后台 Task 异常未捕获 | 在 Task.Run 内 try/catch，更新 session 状态为 Failed，发布 session.ended |
| Registry 内存泄漏 | 执行完成后 Unregister(sessionId)，或 TTL 清理 |
| SSE 连接断开 | 客户端重连；服务端不依赖单连接存活 |
| 工具审批阻塞 Agent 线程 | 使用 TaskCompletionSource 异步等待，不占用线程池 |

## 6. 实施顺序

1. **Phase 1**：ISessionStreamPublisher + GET /stream + 后台执行（analyze/chat 先保持同步可选，或直接改）
2. **Phase 2**：ISessionExecutionRegistry + Interrupt 联动 + 前端 Interrupt 按钮
3. **Phase 3**：Resume 传递 Tracker + 后台执行 + 前端 Resume UI
4. **Phase 4**：ToolExecutor 审批检查 + per-tool API + 前端审批 UI
