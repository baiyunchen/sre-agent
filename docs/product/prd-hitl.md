# PRD: Human-in-the-Loop 完善

- Feature Key: `hitl`
- 版本: 1.0
- 更新日期: 2026-03-14

## 1. 背景与目标

### 1.1 背景

SRE Agent 系统用于自动化分析告警。当前实现存在以下 Human-in-the-Loop 能力缺口：

1. **无法实时观察与打断**：Agent 执行是同步阻塞 HTTP 请求，SessionDetail 使用轮询（15s/30s）获取进度；Interrupt API 仅更新 DB 状态，不会真正停止正在运行的 agent。
2. **无法在中断后继续**：Resume 未传递 IExecutionTracker，恢复后无跟踪；ProvideInput 存储的干预数据 agent 不使用；前端无 Resume UI。
3. **工具审批流程不可用**：ApprovalRule 存在但 ToolExecutor 不检查；ToolInvocation 有审批字段未使用；前端 Approvals 是 session 级别，非 tool 级别。

### 1.2 目标

完善三个 Human-in-the-Loop 能力：

- **功能 1**：人类能实时观察并打断 Agent 执行
- **功能 2**：人类能在 Agent 中断后提供输入，Agent 继续运行
- **功能 3**：工具审批流程可用

## 2. 用户故事与验收标准

### US-H01: 后台执行 + Per-Session SSE

**User Story**：作为 SRE 工程师，我希望 Agent 在后台执行且能通过 per-session SSE 实时观察进度，以便无需轮询即可感知工具调用与执行状态。

**Acceptance Criteria (Given / When / Then)**：

- Given 用户触发 analyze 或 chat，When 请求提交，Then HTTP 立即返回 sessionId（或 202 Accepted），Agent 在后台执行
- Given Agent 正在执行，When 前端连接 `GET /api/sessions/{sessionId}/stream`，Then 持续接收 SSE 事件（工具调用开始/完成、agent thinking、消息等）
- Given session 不存在或已完成，When 连接 stream，Then 返回 404 或建立连接后推送结束事件
- Given 前端 SessionDetail 页面打开，When 订阅 stream，Then 时间线、工具调用等数据实时更新，无需轮询

**范围边界**：

- In scope: 后台执行、per-session SSE、前端订阅与 UI 更新
- Out of scope: 多 tab 共享同一 stream 的优化、重连策略细节

---

### US-H02: 中断机制

**User Story**：作为 SRE 工程师，我希望能实时打断正在执行的 Agent，以便在发现异常时及时止损。

**Acceptance Criteria (Given / When / Then)**：

- Given Agent 正在执行，When 用户点击 Interrupt/Cancel 并调用 `POST /api/sessions/{sessionId}/interrupt`，Then 正在运行的 agent 主循环检测到取消并停止
- Given 已中断的 session，When 查询 session 详情，Then 状态为 Interrupted
- Given 前端 SessionDetail 页面，When session 状态为 Running，Then 显示 Interrupt/Cancel 按钮
- Given 用户点击 Interrupt，When 请求成功，Then 按钮禁用或隐藏，状态更新为 Interrupted

**范围边界**：

- In scope: per-session CancellationTokenSource、Interrupt API 触发取消、ToolLoopAgent 循环检查、前端按钮
- Out of scope: 强制 kill 进程级取消

---

### US-H03: 恢复与人类输入

**User Story**：作为 SRE 工程师，我希望在 Agent 中断后提供输入并 Resume，以便继续分析并补充上下文。

**Acceptance Criteria (Given / When / Then)**：

- Given session 状态为 Interrupted，When 用户输入文本并点击 Resume，Then 调用 `POST /api/sessions/{sessionId}/resume` 携带 continueInput，Agent 从 checkpoint 恢复并后台执行
- Given Resume 成功，When Agent 继续执行，Then 通过 per-session SSE 实时推送进度
- Given 前端 SessionDetail 页面，When session 状态为 Interrupted，Then 显示输入框和 Resume 按钮
- Given ProvideInput 已存储的干预数据，When Resume 时，Then agent 能使用该输入（或与 continueInput 合并）

**范围边界**：

- In scope: Resume 传递 IExecutionTracker、前端 Interrupted 态 UI、Resume 后后台执行
- Out of scope: 多轮 ProvideInput 的复杂合并策略

---

### US-H04: 工具审批流程

**User Story**：作为审批人，我希望在工具执行前进行审批，以便对敏感操作进行人工把关。

**Acceptance Criteria (Given / When / Then)**：

- Given 工具需要审批（无 always-allow 且无 always-deny 规则），When Agent 调用该工具，Then ToolExecutor 暂停执行，session 状态变为 WaitingApproval，ToolInvocation 记录 PendingApproval
- Given 存在待审批工具，When 审批人调用 approve/reject API，Then 工具执行继续或跳过，session 状态恢复
- Given 前端 SessionDetail 页面，When session 有待审批工具，Then 显示工具名称、参数及 Approve/Reject 按钮
- Given 前端 Approvals 页面，When 升级为 tool 级别，Then 可查看待审批工具列表并执行审批

**范围边界**：

- In scope: ToolExecutor 审批检查、per-tool approval API、前端待审批工具 UI、Approvals 页面升级
- Out of scope: 审批超时自动拒绝、审批人权限细分

## 3. 非功能需求

- **性能**：SSE 推送延迟 < 2s（工具调用开始/完成事件）
- **可用性**：Interrupt 响应后 agent 应在下一迭代内停止（通常 < 30s）
- **兼容性**：现有 analyze/chat 调用方需适配 202 或保持同步模式（可配置）

## 4. 范围边界汇总

| 项目 | In Scope | Out of Scope |
|------|----------|--------------|
| 实时观察 | 后台执行、per-session SSE、前端订阅 | 多 tab 共享、重连策略细节 |
| 中断 | CancellationToken 取消、Interrupt API、前端按钮 | 进程级强制 kill |
| 恢复 | Resume + IExecutionTracker、Interrupted 态输入框 | 多轮 ProvideInput 复杂合并 |
| 工具审批 | ToolExecutor 检查、per-tool API、前端升级 | 审批超时、权限细分 |

## 5. 依赖与约束

- 依赖现有：`IExecutionTracker`、`ICheckpointService`、`InterventionService`、`ApprovalRuleEntity`
- 约束：ToolLoopAgent 主循环需支持 CancellationToken 检查；SSE 参考 `GET /api/events/stream` 实现
