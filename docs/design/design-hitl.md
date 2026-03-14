# 设计描述: Human-in-the-Loop UI

- Feature Key: `hitl`
- 版本: 1.0
- 更新日期: 2026-03-14

## 1. 设计原则

- 复用现有 SessionDetailPage、ApprovalsPage 布局与 shadcn/ui 组件
- 状态驱动 UI：根据 session.status 切换展示（Running / Interrupted / WaitingApproval）
- 实时更新：通过 per-session SSE 替代轮询，减少延迟

## 2. SessionDetailPage 变更

### 2.1 状态与交互

| Session 状态 | 输入区展示 | 动作按钮 |
|-------------|-----------|---------|
| Running | 消息输入框 + Send | Interrupt（红色）、Cancel（灰色） |
| Interrupted | 输入框 + Resume | Resume（主色） |
| WaitingApproval | 输入区隐藏或禁用 | 无（审批在 Tools 面板） |
| Completed / Failed / Cancelled | 输入区隐藏 | 无 |

### 2.2 Interrupt / Cancel 按钮

- **位置**：Header 区域，与 sessionId 同一行右侧
- **样式**：`Button variant="destructive"`（Interrupt）、`Button variant="outline"`（Cancel）
- **图标**：StopCircle（Interrupt）、XCircle（Cancel）
- **可见条件**：`session.status === "Running"`
- **交互**：点击后调用 `POST /api/sessions/{id}/interrupt` 或 `cancel`，成功后状态更新为 Interrupted/Cancelled

### 2.3 Resume 输入区

- **位置**：与现有消息输入区相同（底部 border-t 区域）
- **可见条件**：`session.status === "Interrupted"`
- **组件**：Input（placeholder: "Provide additional context for the agent..."）+ Button "Resume"
- **交互**：调用 `POST /api/sessions/{id}/resume` 携带 `continueInput`，成功后状态变为 Running，通过 SSE 实时观察

### 2.4 SSE 订阅与实时更新

- **Hook**：`useSessionStream(sessionId)` 订阅 `GET /api/sessions/{id}/stream`
- **行为**：收到 `tool.started` / `tool.completed` 等事件时，invalidate `timeline`、`tool-invocations` 等 query
- **连接状态**：可选在 Header 或 Card 角标显示 "Live" 或连接图标（参考 useDashboardStream）
- **空态**：session 已完成时 stream 可能立即结束，不显示连接状态

### 2.5 Tools 面板 - 待审批工具

- **扩展**：ToolInvocationSummary 增加 `approvalStatus` 字段
- **待审批项**：`approvalStatus === "PendingApproval"` 时，显示 Approve / Reject 按钮
- **布局**：每行工具调用，若为 PendingApproval，右侧显示 [Approve] [Reject]
- **交互**：调用 `POST /api/sessions/{id}/tool-invocations/{invocationId}/approve` 或 `reject`

## 3. ApprovalsPage 变更

### 3.1 升级为 Tool 级别

- **Pending 列表**：从 session 级别改为 tool-invocation 级别
- **数据结构**：每项包含 sessionId、invocationId、toolName、parameters、requestedAt
- **API**：`GET /api/approvals/pending-tools`（或复用现有 pending 扩展 schema）
- **审批动作**：调用 per-tool approve/reject API

### 3.2 列表展示

- **列**：Session ID（可点击跳转）、Tool Name、Parameters 摘要、Requested At、操作
- **操作**：Approve、Reject 按钮，与 SessionDetail 一致

## 4. 组件复用

- Button、Badge、Card、Input、Tabs：已有
- 无需新增专用组件，仅扩展现有页面逻辑

## 5. 错误与空态

- **Stream 连接失败**：静默重连或显示 "Connection lost" 提示
- **Interrupt 失败**：Toast 或 inline 错误信息
- **Resume 失败**：同上
- **无待审批工具**：Approvals 页面 Pending 列表为空时显示 "No pending approvals"
