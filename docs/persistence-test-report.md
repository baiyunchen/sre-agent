# 持久化功能测试报告

**测试日期**: 2026-03-10  
**测试基准**: `docs/07-执行过程持久化.md` 设计文档  
**测试方法**: 代码审计 + API 调用 + 数据库验证  

---

## 1. 测试摘要

| 功能模块 | 数据表 | 设计状态 | 代码实现 | 数据验证 | 结论 |
|---------|--------|---------|---------|---------|------|
| 会话管理 | `sessions` | ✅ 已设计 | ⚠️ 部分实现 | ⚠️ 字段大量为空 | **缺陷** |
| 消息持久化 | `messages` | ✅ 已设计 | ✅ 已实现 | ✅ 数据正常 | **通过** |
| Agent 运行记录 | `agent_runs` | ✅ 已设计 | ❌ 未集成 | ❌ 表为空 | **缺陷** |
| 工具调用记录 | `tool_invocations` | ✅ 已设计 | ❌ 未集成 | ❌ 表为空 | **缺陷** |
| 检查点机制 | `checkpoints` | ✅ 已设计 | ❌ 写入未调用 | ❌ 表为空 | **缺陷** |
| 人工干预 | `interventions` | ✅ 已设计 | ⚠️ 部分实现 | ✅ 有数据 | **部分缺陷** |
| 审计日志 | `audit_logs` | ✅ 已设计 | ❌ 写入未调用 | ❌ 表为空 | **缺陷** |
| 诊断数据存储 | `diagnostic_data` | ✅ 已设计 | ❌ 写入未调用 | ❌ 表为空 | **缺陷** |

**总结: 8 项功能中仅 1 项完全通过，1 项部分通过，6 项存在功能缺陷。**

---

## 2. 测试环境

- **API 地址**: `http://localhost:5099`
- **数据库**: PostgreSQL `sre_agent@localhost:5432/sre_agent`
- **测试 Session ID**: `2f888d3e-48d0-433e-9454-8553a49e8800`

---

## 3. 逐项测试详情

### 3.1 sessions 表 — ⚠️ 部分实现

#### 测试方法
1. 调用 `POST /api/sre/analyze` 创建新会话
2. 查询 `sessions` 表验证字段填充情况
3. 调用 `GET /api/Session/{id}` 查看 API 返回

#### 数据库实际数据

```sql
SELECT * FROM sessions WHERE id = '2f888d3e-48d0-433e-9454-8553a49e8800';
```

| 字段 | 期望值 | 实际值 | 通过 |
|------|-------|--------|------|
| id | UUID | `2f888d3e-48d0-433e-9454-8553a49e8800` | ✅ |
| status | Running → Completed | `Running`（执行完仍为 Running） | ❌ |
| alert_id | 告警标识 | `空` | ❌ |
| alert_name | `persistence-test-alarm`（来自 request.Title） | `空` | ❌ |
| alert_data | 告警完整数据 JSON | `空` | ❌ |
| service_name | `test-service`（来自 request.AffectedService） | `空` | ❌ |
| service_metadata | 服务元数据 | `空` | ❌ |
| current_agent_id | 当前 Agent ID | `空` | ❌ |
| current_step | 当前步骤 | `0` | ❌ |
| execution_state | 执行状态 | `空` | ❌ |
| diagnosis | 诊断结果 JSON | `空` | ❌ |
| diagnosis_summary | 诊断摘要 | `空` | ❌ |
| confidence | 置信度 | `空` | ❌ |
| created_at | 创建时间 | `2026-03-10T15:15:49` | ✅ |
| started_at | 开始执行时间 | `空` | ❌ |
| completed_at | 完成时间 | `空` | ❌ |
| updated_at | 更新时间 | `2026-03-10T15:15:49` | ✅ |

#### 根因分析

`PostgresContextStore.SaveAsync` 创建 Session 时只设置了 4 个字段：

```csharp
session = new SessionEntity
{
    Id = snapshot.SessionId,
    Status = "Running",     // 永远是 Running，从不更新为 Completed/Failed
    CreatedAt = snapshot.CreatedAt,
    UpdatedAt = DateTime.UtcNow
};
```

**问题清单**:
1. `AnalyzeRequest` 中的 `Title` → `AlertName`、`AffectedService` → `ServiceName` 映射未实现
2. Session 状态永远停留在 `Running`，执行完成后不更新为 `Completed` 或 `Failed`
3. `StartedAt`、`CompletedAt` 时间戳从未设置
4. Agent 执行结果（`Diagnosis`, `DiagnosisSummary`, `Confidence`）未回写到 Session
5. 执行过程状态（`CurrentAgentId`, `CurrentStep`, `ExecutionState`）未更新

---

### 3.2 messages 表 — ✅ 通过

#### 测试方法
1. 调用 analyze API 后查询 messages 表

#### 数据库验证

```sql
SELECT count(*), string_agg(DISTINCT role, ', ') FROM messages 
WHERE session_id = '2f888d3e-48d0-433e-9454-8553a49e8800';
```

| 检查项 | 期望 | 实际 | 通过 |
|--------|-----|------|------|
| 消息数量 | > 0 | 22 条 | ✅ |
| 角色类型 | User, Assistant, Tool | `Assistant, Tool, User` | ✅ |
| Parts JSONB | 非空 | 正常序列化 | ✅ |
| Metadata JSONB | 非空 | 正常序列化 | ✅ |
| EstimatedTokens | > 0 | 有值 | ✅ |
| AgentId | 有值 | `sre-coordinator`（Assistant 消息） | ✅ |
| CreatedAt | 有值 | 正常时间戳 | ✅ |

**结论**: 消息持久化功能正常。`PostgresContextStore` 正确地将 `ContextSnapshot.Messages` 序列化并存储。

---

### 3.3 agent_runs 表 — ❌ 未实现

#### 测试方法
1. 调用 analyze API 后查询 agent_runs 表

#### 数据库验证

```sql
SELECT count(*) FROM agent_runs WHERE session_id = '2f888d3e-48d0-433e-9454-8553a49e8800';
-- 结果: 0
```

#### 代码审计

| 检查项 | 状态 |
|--------|------|
| `AgentRunEntity` 实体类 | ✅ 已定义 |
| `IAgentRunRepository` 接口 | ✅ 已定义 |
| `AgentRunRepository` 实现 | ✅ 已实现 (CreateAsync, UpdateAsync, GetBySessionAsync) |
| DI 注册 | ✅ 已注册 |
| **任何地方注入使用** | ❌ **从未被注入或使用** |

#### 根因分析

设计文档 §4.1 要求在 Agent 每次执行时创建 `AgentRun` 记录。但：
- `ToolLoopAgent.ExecuteAsync` 不依赖任何持久化服务
- `ToolLoopAgent` 直接 `new ToolExecutor(_logger)` 创建工具执行器，没有注入点
- 执行管道中没有 hook/middleware/event 来记录 Agent 运行

**影响**: 无法追溯 Agent 的执行历史、输入输出、状态和耗时。

---

### 3.4 tool_invocations 表 — ❌ 未实现

#### 测试方法
1. 调用 analyze API 后查询 tool_invocations 表（Agent 执行中调用了 11 次工具）

#### 数据库验证

```sql
SELECT count(*) FROM tool_invocations;
-- 结果: 0
```

#### 代码审计

| 检查项 | 状态 |
|--------|------|
| `ToolInvocationEntity` 实体类 | ✅ 已定义 |
| `IToolInvocationRepository` 接口 | ✅ 已定义 |
| `ToolInvocationRepository` 实现 | ✅ 已实现 |
| DI 注册 | ✅ 已注册 |
| **任何地方注入使用** | ❌ **从未被注入或使用** |

#### 根因分析

与 `agent_runs` 相同——`ToolExecutor` 在执行工具时不调用任何持久化服务。设计文档 §4.2 要求每次工具调用都记录参数、结果、审批状态和耗时，但执行管道完全没有集成。

**影响**: 无法审计工具调用历史。本次测试中 Agent 执行了 11 轮迭代、多次工具调用，但数据库中 0 条记录。

---

### 3.5 checkpoints 表 — ❌ 写入未调用

#### 测试方法
1. 调用 analyze API 创建会话
2. 调用 `POST /api/Session/{id}/interrupt` 中断会话
3. 查询 checkpoints 表
4. 调用 `POST /api/Session/{id}/resume` 尝试恢复

#### 数据库验证

```sql
SELECT count(*) FROM checkpoints WHERE session_id = '2f888d3e-48d0-433e-9454-8553a49e8800';
-- 结果: 0（中断后仍为 0）
```

#### Resume 测试

```bash
curl -s -X POST http://localhost:5099/api/Session/2f888d3e-48d0-433e-9454-8553a49e8800/resume \
  -H "Content-Type: application/json" -d '{"input": "continue analysis"}'
```

**返回**: `{"error":"No checkpoint found for session 2f888d3e-48d0-433e-9454-8553a49e8800"}`

#### 代码审计

| 检查项 | 状态 |
|--------|------|
| `CheckpointEntity` 实体类 | ✅ 已定义 |
| `CheckpointRepository` | ✅ 已实现 |
| `CheckpointService.CreateCheckpointAsync` | ✅ 已实现 |
| `CheckpointService.RestoreFromCheckpointAsync` | ✅ 已实现 |
| **CreateCheckpointAsync 被调用** | ❌ **从未被调用** |

#### 根因分析

设计文档 §6.2 要求 `InterventionService.InterruptSessionAsync` 在中断时创建检查点：

```csharp
// 设计文档中的期望：
await _checkpointService.CreateCheckpointAsync(session, _contextManager, "interrupt");
```

但实际的 `InterventionService` 没有：
1. 注入 `ICheckpointService`
2. 注入 `IContextManager`（无法获取正在运行的上下文）
3. 调用 `CreateCheckpointAsync`

**影响**: 
- 中断后无法恢复会话（`resume` 必定失败）
- 断点续跑功能完全不可用
- `SessionRecoveryService.ResumeSessionAsync` 的前置条件永远不满足

---

### 3.6 interventions 表 — ⚠️ 部分实现

#### 测试方法
1. 调用 `POST /api/Session/{id}/interrupt` 中断会话
2. 查询 interventions 表

#### 数据库验证

```sql
SELECT * FROM interventions WHERE session_id = '2f888d3e-48d0-433e-9454-8553a49e8800';
```

| 字段 | 期望 | 实际 | 通过 |
|------|-----|------|------|
| type | Interrupt | `Interrupt` | ✅ |
| reason | 中断原因 | `Test interrupt for persistence verification` | ✅ |
| intervened_by | 用户 ID | `test-user` | ✅ |
| intervened_at | 时间戳 | `2026-03-10T15:16:09` | ✅ |

#### 部分通过原因

interventions 表本身的写入正常，但设计文档中要求中断时同时：
1. ✅ 创建 intervention 记录
2. ✅ 更新 session 状态为 Interrupted
3. ❌ 创建 checkpoint（未实现）
4. ❌ 发布 `SessionInterruptedEvent`（事件总线未实现）
5. ❌ 写入 audit_log（审计日志未实现）

---

### 3.7 audit_logs 表 — ❌ 写入未调用

#### 测试方法
1. 执行 analyze、interrupt 等操作后查询 audit_logs 表

#### 数据库验证

```sql
SELECT count(*) FROM audit_logs;
-- 结果: 0
```

#### API 验证

```bash
curl -s http://localhost:5099/api/Session/2f888d3e-48d0-433e-9454-8553a49e8800/audit
# 返回: []
```

#### 代码审计

| 检查项 | 状态 |
|--------|------|
| `AuditLogEntity` 实体类 | ✅ 已定义 |
| `AuditLogRepository` | ✅ 已实现 |
| `AuditService.LogAsync` | ✅ 已实现 |
| `AuditService.GetBySessionAsync` | ✅ 已实现且被 SessionController 使用 |
| **LogAsync 被调用** | ❌ **从未被调用** |

#### 根因分析

设计文档 §8.2 描述了事件驱动的审计日志方案：
- `AuditEventHandler` 订阅 `ToolExecutionCompletedEvent`、`SessionInterruptedEvent` 等事件
- 每个事件处理器调用 `AuditService.LogAsync`

但当前代码中：
1. 没有 `IEventBus` / 事件总线的实现
2. 没有 `AuditEventHandler` 类
3. 没有任何地方发布事件
4. `SessionController` 的 interrupt/cancel/resume 操作也不写审计日志

**影响**: 审计追溯功能完全不可用。只有空的查询 API 但无数据。

---

### 3.8 diagnostic_data 表 — ❌ 写入未调用

#### 测试方法
1. 执行 analyze API（Agent 调用了 CloudWatch 工具）后查询 diagnostic_data 表

#### 数据库验证

```sql
SELECT count(*) FROM diagnostic_data;
-- 结果: 0
```

#### 代码审计

| 检查项 | 状态 |
|--------|------|
| `DiagnosticDataEntity` 实体类 | ✅ 已定义 |
| `DiagnosticDataRepository` | ✅ 已实现 |
| `DiagnosticDataService.StoreBatchAsync` | ✅ 已实现 |
| `SearchDiagnosticDataTool` | ✅ 已实现（但查无数据） |
| `GetDiagnosticSummaryTool` | ✅ 已实现（但查无数据） |
| `QueryDiagnosticDataTool` | ✅ 已实现（但查无数据） |
| **StoreBatchAsync 被调用** | ❌ **从未被调用** |

#### 根因分析

设计文档 §9.7 要求 CloudWatch 工具在查询结果超过阈值（如 20 条）时：
1. 将结果存入 `diagnostic_data` 表
2. 只返回摘要到 Agent 上下文
3. Agent 通过 `search_diagnostic_data` 等工具按需检索

但当前 `CloudWatchSimpleQueryTool` 和 `CloudWatchInsightsQueryTool`：
1. 不注入 `IDiagnosticDataService`
2. 没有阈值检查逻辑
3. 永远将全部结果直接返回到上下文

**影响**:
- 大量日志直接进入上下文，消耗大量 Token
- 上下文可能溢出
- `search_diagnostic_data`、`query_diagnostic_data`、`get_diagnostic_summary` 三个工具形同虚设

---

## 4. 缺陷汇总

### 4.1 严重缺陷（功能完全不可用）

| # | 缺陷描述 | 影响范围 | 关联表 |
|---|---------|---------|--------|
| D-01 | Agent 运行记录未写入 | agent_runs 表永远为空，无法追溯执行历史 | `agent_runs` |
| D-02 | 工具调用记录未写入 | tool_invocations 表永远为空，无法审计工具使用 | `tool_invocations` |
| D-03 | 中断时未创建 Checkpoint | 会话恢复功能完全不可用，resume 必定报错 | `checkpoints` |
| D-04 | 审计日志写入未实现 | audit_logs 表永远为空，无法审计追溯 | `audit_logs` |
| D-05 | 诊断数据存储未集成 | 大量日志直接撑入上下文，诊断查询工具无数据 | `diagnostic_data` |

### 4.2 中等缺陷（功能部分可用但数据不完整）

| # | 缺陷描述 | 影响范围 | 关联表 |
|---|---------|---------|--------|
| D-06 | Session 告警字段未填充 | AlertName, ServiceName 等字段为空，按告警查询不可用 | `sessions` |
| D-07 | Session 状态不更新 | 执行完成后仍为 Running，Status 无意义 | `sessions` |
| D-08 | Session 时间戳缺失 | StartedAt, CompletedAt 永远为空 | `sessions` |
| D-09 | Session 诊断结果未回写 | Diagnosis, DiagnosisSummary, Confidence 为空 | `sessions` |
| D-10 | 事件总线未实现 | 设计中的事件驱动审计/通知机制不存在 | 跨多表 |

### 4.3 低等缺陷

| # | 缺陷描述 | 影响范围 |
|---|---------|---------|
| D-11 | SessionRepository.CreateAsync 从未使用 | 冗余代码 |
| D-12 | GetByAlertAsync 因 AlertId 为空无法命中 | 查询功能不可用 |

---

## 5. 设计文档 vs 实现状态 对照矩阵

| 设计文档章节 | 描述 | 实现状态 |
|------------|------|---------|
| §2 会话状态管理 | 完整的状态机 (Created→Running→Completed/Failed/...) | ❌ 只有 Running→Interrupted→Running |
| §3 消息持久化 | Message + MessagePart 的 JSONB 存储 | ✅ 已实现 |
| §4.1 Agent 运行实体 | 每次 Agent 执行创建 AgentRun | ❌ Repository 存在但未集成到执行管道 |
| §4.2 工具调用记录 | 每次工具调用创建 ToolInvocation | ❌ Repository 存在但未集成到执行管道 |
| §5.1 上下文快照 | ContextSnapshot 持久化 | ✅ 已实现（PostgresContextStore） |
| §5.3 检查点机制 | 中断/异常时创建 Checkpoint | ❌ CreateCheckpointAsync 存在但从未被调用 |
| §5.4 恢复服务 | 从 Checkpoint 恢复执行 | ❌ 代码存在但因无 Checkpoint 数据无法工作 |
| §6 人工干预 | 中断/取消/提供输入 + 创建检查点 | ⚠️ 干预记录已实现，但不创建检查点 |
| §8 审计日志 | 事件驱动的审计记录 | ❌ 事件总线和 Handler 均未实现 |
| §9 诊断数据存储 | CloudWatch 大结果存 DB + 按需查询 | ❌ 工具未集成 DiagnosticDataService |
| §10 数据清理 | TTL 清理过期诊断数据和旧 Checkpoint | ⚠️ DataCleanupService 存在但因无数据无实际效果 |

---

## 6. 建议修复优先级

### P0（核心功能阻断）
1. **Session 状态管理**: 执行完成后更新状态为 Completed/Failed，填充告警字段和诊断结果
2. **Checkpoint 创建**: InterventionService 中断时创建 Checkpoint，使会话恢复可用

### P1（审计合规要求）
3. **Agent 运行记录**: 在 Agent 执行管道中集成 AgentRun 持久化
4. **工具调用记录**: 在工具执行管道中集成 ToolInvocation 持久化
5. **审计日志**: 在关键操作点调用 AuditService.LogAsync

### P2（性能和可用性）
6. **诊断数据存储**: CloudWatch 工具集成 DiagnosticDataService，大结果存 DB 返回摘要

---

## 附录: 测试数据快照

### A. 全表行数统计

```
    table_name    | row_count 
------------------+-----------
 sessions         |         7
 messages         |       205
 agent_runs       |         0
 tool_invocations |         0
 checkpoints      |         0
 interventions    |         1
 audit_logs       |         0
 diagnostic_data  |         0
```

### B. 测试 Session 完整数据

```
id:               2f888d3e-48d0-433e-9454-8553a49e8800
status:           Interrupted (中断后更新，执行完成时仍为 Running)
alert_id:         空
alert_name:       空 (期望: persistence-test-alarm)
service_name:     空 (期望: test-service)
current_agent_id: 空
current_step:     0
diagnosis:        空
confidence:       空
created_at:       2026-03-10T15:15:49
started_at:       空
completed_at:     空
updated_at:       2026-03-10T15:16:09
```

### C. 测试消息统计

```
session_id: 2f888d3e-48d0-433e-9454-8553a49e8800
总消息数:    22
角色分布:    Assistant, Tool, User
```
