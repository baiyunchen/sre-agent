# Code Review Report: HITL Phase 1–4 变更

**评审范围**: 最近 4 个 commits (6098814, 3af0be5, 57ef0fa, 84a2630)  
**评审日期**: 2026-03-14  
**评审人**: Code Reviewer Agent

---

## 1. 总体评价

本次 HITL 变更实现了后台执行、per-session SSE 流、中断/恢复、工具审批等核心能力，整体架构与 `docs/architecture/arch-hitl.md` 基本一致，代码结构清晰。但存在若干**关键缺陷**需要修复后方可进入 QA：

- **Critical**: Session 状态未在工具审批时更新为 `WaitingApproval`，与架构/PRD 不符
- **Critical**: `SessionExecutionRegistry.Register` 重复注册时存在 CTS 泄漏与旧任务未清理
- **Major**: `SessionStreamPublisher` 多订阅者场景下 channel 被覆盖，可能导致事件丢失
- **Major**: `ToolApprovalService.RequestApprovalAsync` 无超时，存在长期阻塞与 TCS 泄漏风险

**结论**: **Request Changes** — 需修复 Critical 与 Major 问题后再进入 QA。

---

## 2. 各文件具体发现

### 2.1 `ToolApprovalService.cs`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Critical** | 未将 session 状态更新为 `WaitingApproval`。架构文档与 PRD 要求：需要审批时 session 置为 `WaitingApproval`。当前仅更新了 invocation 的 `ApprovalStatus`，导致 `GET /api/approvals/pending` 无法查到，前端 `showResumeUI` 对 `WaitingApproval` 的判断失效 | 在 `RequestApprovalAsync` 中通过 scope 获取 `ISessionRepository`，将对应 session 的 `Status` 更新为 `"WaitingApproval"`；在 `ResolveApprovalAsync` 中将其恢复为 `"Running"` |
| **Major** | `RequestApprovalAsync` 无超时，若用户长期不审批，TCS 会一直保留在 `_pendingApprovals` 中，造成阻塞与潜在内存泄漏 | 使用 `CancellationTokenSource.CreateLinkedTokenSource(ct, new CancellationTokenSource(TimeSpan.FromMinutes(30)).Token)` 或 `Task.WhenAny(tcs.Task, Task.Delay(timeout))` 增加超时；超时时 `TrySetCanceled` 并 `TryRemove` |
| **Info** | `HasPendingApproval` 使用 `ContainsKey`，在高并发下与 `TryGetValue` 存在 TOCTOU，但对当前场景影响有限 | 可保持现状，或改为 `TryGetValue` 并检查非 null |

### 2.2 `SessionExecutionRegistry.cs`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Critical** | `Register` 直接 `_sources[sessionId] = cts`，若同一 session 被重复启动（如双击、重试），会覆盖旧 CTS 且未 Dispose，造成泄漏；旧 Task 仍会继续执行 | 在 `Register` 中先 `TryRemove` 旧 CTS 并 `Dispose`；或返回 `bool` 表示是否覆盖，由调用方决定是否允许重复启动 |
| **Minor** | `Unregister` 中 `catch { }` 吞掉异常，不利于排查 | 至少记录 `_logger.LogWarning` |

### 2.3 `SessionStreamPublisher.cs`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Major** | 多订阅者时，后一次 `SubscribeAsync` 会覆盖 `_channels[sessionId]`，前一个 channel 被孤立；前一个客户端断开时 `finally` 中 `TryRemove` 会移除当前（第二个）订阅者的 channel，导致事件丢失 | 使用 `ConcurrentDictionary<Guid, List<ChannelWriter<SessionStreamEvent>>>` 或广播 channel，支持多订阅者；或明确文档说明“每 session 仅支持单订阅者” |
| **Minor** | Channel 由 `Subscribe` 创建，若 agent 在客户端订阅前已开始，`agent.started` 等早期事件会被丢弃（`PublishAsync` 中 `TryGetValue` 失败） | 架构已接受“客户端重连”，可补充文档说明；或由 `BackgroundExecutor` 在启动时预先创建 channel |

### 2.4 `BackgroundSessionExecutor.cs`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | DI scope 处理正确：使用 `IServiceScopeFactory.CreateScope()` 创建 scope，从 scope 解析 `IAgent`、`IExecutionTracker` 等 scoped 服务 | 无 |
| **Info** | 异常处理符合架构：`catch (Exception)` 中更新 session 为 Failed、发布 `session.ended` | 无 |
| **Minor** | `catch (OperationCanceledException)` 中未更新 session 状态为 `Cancelled` 或 `Interrupted`，仅写 audit log | 可考虑调用 `contextStore.SaveAsync` 将 status 更新为 `Cancelled`，与 `InterventionService.CancelSessionAsync` 行为一致 |

### 2.5 `StreamingExecutionTracker.cs`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Minor** | `PublishAsync` 若抛出（如 channel 已关闭），异常会向上传播到 agent 循环，最终由 `BackgroundSessionExecutor` 捕获 | 可考虑在 publish 处 `try/catch` 并记录 warning，避免 publish 失败影响 agent 主流程 |
| **Info** | `run == null` 或 `invocation == null` 时不发布事件，逻辑合理 | 无 |

### 2.6 `SessionController.cs` / SSE 端点

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | SSE 响应头设置正确：`Cache-Control: no-cache`、`Connection: keep-alive`、`X-Accel-Buffering: no` | 无 |
| **Info** | 客户端断开时 `OperationCanceledException` 被正确捕获 | 可增加 `_logger.LogDebug` 便于排查 |
| **Info** | approve/reject 端点与 OpenAPI 契约一致 | 无 |

### 2.7 `useSessionStream.ts`（前端）

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | `EventSource` 默认会重连，`onerror` 时仅更新 status 为 `disconnected` | 可考虑在 `onerror` 后增加重连策略或提示用户 |
| **Minor** | `JSON.parse(e.data)` 失败时静默忽略，可能掩盖 payload 格式问题 | 可增加 `console.warn` 便于调试 |
| **Info** | `invalidateQueries` 在各类事件上触发，能及时刷新 timeline/tool-invocations | 无 |

### 2.8 `SessionDetailPage.tsx`

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | `streamEnabled = sessionStatus === "Running"` 符合设计；`WaitingApproval` 时依赖 `pendingApproval` 状态展示审批 UI | 需配合修复 ToolApprovalService 中 session 状态更新 |
| **Info** | Interrupt/Cancel/Resume/Approve/Reject 按钮与 API 调用正确 | 无 |

### 2.9 `PersistenceServiceExtensions.cs`（DI 注册）

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | `BackgroundSessionExecutor`、`SessionExecutionRegistry`、`SessionStreamPublisher`、`ToolApprovalService` 注册为 Singleton 符合设计 | 无 |
| **Info** | `IExecutionTracker` 使用 `StreamingExecutionTracker` 装饰 `PersistenceExecutionTracker`，scoped 解析正确 | 无 |

### 2.10 API 契约一致性

| 严重程度 | 发现 | 建议 |
|----------|------|------|
| **Info** | `GET /api/sessions/{sessionId}/stream`、`POST .../approve`、`POST .../reject` 与 `openapi.yaml` 一致 | 无 |
| **Info** | `SessionStreamEvent` 的 `eventType` 包含 `tool.approval_required`，与实现一致 | 无 |
| **Info** | `interrupt`、`resume`、`cancel` 端点在 arch 中标注为“已有端点”，OpenAPI 中未显式定义，可后续补充 | 建议在 openapi.yaml 中补充这三个端点以保持契约完整 |

---

## 3. 架构契合度

| 架构决策 | 实现情况 |
|----------|----------|
| 后台执行模型（202 + Task.Run） | ✅ 已实现 |
| Per-session SSE（`GET /stream`） | ✅ 已实现 |
| 中断机制（Registry.Cancel + CTS） | ✅ 已实现 |
| 恢复执行（PrepareResumeAsync + 后台执行） | ✅ 已实现 |
| 工具审批（ToolExecutor 检查 + per-tool API） | ⚠️ 缺少 session 状态更新 |
| Registry 执行完成后 Unregister | ✅ 已实现 |
| 后台 Task 异常捕获与 session.ended 发布 | ✅ 已实现 |

---

## 4. 测试充分性

| 项目 | 现状 | 建议 |
|------|------|------|
| 单元测试 | `SessionControllerTests` 已适配 202 + `BackgroundExecutor` mock | 建议增加 `ToolApprovalService`、`SessionStreamPublisher`、`SessionExecutionRegistry` 的单元测试 |
| 集成测试 | 未见 HITL 相关集成测试 | 建议增加：后台执行 + SSE 订阅、Interrupt、Resume、工具审批的集成测试 |
| 覆盖率 | 未在本次 diff 中体现 | 按规则需满足变更模块 >= 85% |

---

## 5. 建议改进项（优先级排序）

1. **【必须】修复 ToolApprovalService**：在 `RequestApprovalAsync` 中更新 session 为 `WaitingApproval`，在 `ResolveApprovalAsync` 中恢复为 `Running`。
2. **【必须】修复 SessionExecutionRegistry**：`Register` 时若已存在旧 CTS，先 Dispose 再覆盖；或拒绝重复注册并返回明确错误。
3. **【建议】ToolApprovalService 超时**：为 `RequestApprovalAsync` 增加可配置超时（如 30 分钟），超时后取消并清理 TCS。
4. **【建议】SessionStreamPublisher 多订阅者**：支持多订阅者或明确文档说明单订阅者限制。
5. **【可选】补充 OpenAPI**：将 `interrupt`、`resume`、`cancel` 端点加入 openapi.yaml。
6. **【可选】补充测试**：为 HITL 核心服务与流程增加单元/集成测试。

---

## 6. 结论

| 项目 | 结果 |
|------|------|
| **是否通过** | **Request Changes** |
| **阻塞项** | 1) Session 状态未更新为 WaitingApproval；2) Registry 重复注册导致 CTS 泄漏 |
| **建议完成修复后再进入 QA** | 是 |
