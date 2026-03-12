---
name: Frontend Backend Gap Analysis
overview: 对比 Figma 设计稿（React + shadcn/ui）与当前 .NET 后端实现之间的差距，识别数据模型不匹配、缺失 API、交互逻辑差异，并给出完整的 API 列表和前端实施方案。
todos:
  - id: figma-design-baseline
    content: 锁定 Figma 设计基线（URL/fileKey/MCP 资源清单），作为前端唯一实现依据
    status: done
  - id: gap-analysis
    content: 完成前后端差距分析（数据模型、API、交互逻辑）
    status: done
  - id: backend-schema
    content: 后端数据模型调整：Session 增加 source/severity 字段、定义 Diagnosis schema、新增 ApprovalRule 实体、Todo 持久化
    status: in_progress
  - id: backend-api-p0
    content: 后端 P0 API 实现：sessions 列表、session timeline、diagnosis、tool-invocations、todos、send message
    status: done
  - id: backend-api-p1
    content: 后端 P1 API 实现：dashboard stats/activities/active-sessions、SSE 端点、approval CRUD、tools/agents 注册表
    status: in_progress
  - id: frontend-scaffold
    content: 前端项目搭建：从 Figma Make 导出、建立 API 层、安装 TanStack Query、对齐类型定义
    status: done
  - id: frontend-core
    content: 前端核心页面联调：Sessions + SessionDetail + Dashboard，对接 P0 API + SSE
    status: in_progress
  - id: frontend-secondary
    content: 前端次要页面：Approvals + Tools + Agents + Settings，对接 P1/P2 API
    status: pending
isProject: false
---

# SRE Agent Dashboard: 前后端差距分析与实施方案

---

## 0. 设计基线（新增）

- **Figma 设计源（唯一基线）**：`https://www.figma.com/make/ZhSl72jDQYMWwYCa02IApx/SRE-Agent-Dashboard-Design?p=f&t=uuOKDKM4eBxJH2EH-0`
- **Make File Key**：`ZhSl72jDQYMWwYCa02IApx`
- **实现约束**：
  - 前端页面结构、路由信息架构、关键组件命名以该 Figma Make 为准
  - 新增/调整前端页面前，优先通过 Figma MCP 获取对应上下文
  - 若设计变更，以 Figma 最新版本为准并同步更新本计划
- **MCP 获取结果（已完成）**：
  - 已获取 Make 源资源索引（包含 `src/app/routes.ts`、`src/app/layout/AppLayout.tsx`、7 个页面与组件资源）
  - 已提取核心参考文件：`App.tsx`、`routes.ts`、`AppLayout.tsx`、`Dashboard.tsx`、`Sessions.tsx`

---

## 一、前端设计概览

Figma Make 设计稿是一个完整的 React + Vite + Tailwind + shadcn/ui 应用，包含 **7 个页面**：

- **Dashboard** (`/`) -- 统计卡片、实时活动流、活跃会话、图表
- **Sessions** (`/sessions`) -- 会话列表 + 筛选/搜索/分页
- **Session Detail** (`/sessions/:id`) -- 告警信息、Agent 执行时间线/对话流、诊断面板、工具面板、Todo 面板、反馈
- **Approvals** (`/approvals`) -- 待审批列表、审批历史、永久规则管理
- **Tools** (`/tools`) -- 工具注册表 + Agent 注册表（含统计数据）
- **Agents** (`/agents`) -- Agent 列表 + 性能指标
- **Settings** (`/settings`) -- 通用/LLM/Slack/告警解析/审批/可观测性/数据库配置

此外还有全局组件：`CommandPalette`（全局搜索）、`NotificationDropdown`（通知中心）、`ConnectionStatus`（连接状态）。

---

## 二、后端现状

当前后端仅有 **3 个 Controller、9 个 API 端点**：


| Controller          | 端点                                | 方法   |
| ------------------- | --------------------------------- | ---- |
| `SreController`     | `/api/sre/chat`                   | POST |
| `SreController`     | `/api/sre/analyze`                | POST |
| `SessionController` | `/api/session/{id}`               | GET  |
| `SessionController` | `/api/session/{id}/interrupt`     | POST |
| `SessionController` | `/api/session/{id}/cancel`        | POST |
| `SessionController` | `/api/session/{id}/resume`        | POST |
| `SessionController` | `/api/session/{id}/interventions` | GET  |
| `SessionController` | `/api/session/{id}/audit`         | GET  |
| `HealthController`  | `/health`                         | GET  |


数据库有 8 张表：`sessions`, `messages`, `agent_runs`, `tool_invocations`, `checkpoints`, `interventions`, `audit_logs`, `diagnostic_data`。

---

## 三、前后端不匹配分析

### A. 数据模型不匹配

#### A1. Session Status 枚举不一致

- **前端**: `Running | Completed | Failed | WaitingApproval | Cancelled | TimedOut`
- **后端**: `Created | Running | Completed | Failed | Interrupted | Cancelled | WaitingApproval`（字符串，非枚举）
- **差异**:
  - 前端有 `TimedOut`，后端没有
  - 后端有 `Created` 和 `Interrupted`，前端未设计展示
  - 建议：后端补充 `TimedOut`；前端补充对 `Created`/`Interrupted` 的展示

#### A2. Session 缺少 `source` 和 `severity` 字段

前端 Session 类型定义了：

```typescript
source: 'CloudWatch' | 'Prometheus' | 'Slack Manual';
severity: 'Critical' | 'Warning' | 'Info';
```

后端 `SessionEntity` 没有这两个字段。`AlertData`（JSONB）可能包含，但没有显式字段。

**建议**: 在 `SessionEntity` 上增加 `AlertSource` 和 `AlertSeverity` 字段，或在 `AlertData` JSONB 中约定标准结构。

#### A3. Session 缺少 `agentSteps` 和 `duration` 计算字段

- 前端需要 `agentSteps`（步骤数）-- 后端无直接字段，需从 `AgentRunEntity` 数量或 `ToolInvocationEntity` 数量聚合
- 前端需要 `duration`（秒）-- 后端有 `StartedAt`/`CompletedAt`，可计算但 API 未返回

#### A4. Diagnosis 结构未定义

前端期望：

```typescript
interface Diagnosis {
  hypothesis: string;
  confidence: number;
  evidence: string[];
  recommendedActions: string[];
}
```

后端 `SessionEntity.Diagnosis` 是 `JsonDocument`，结构未约定。需要**定义一个标准的 Diagnosis JSON Schema** 并确保 Agent 按此格式输出。

#### A5. Todo 模型缺少 `createdAt`

前端 `Todo` 有 `createdAt` 字段，后端 `TodoItem` 模型需确认是否包含。此外，后端 `TodoService` 是内存实现（`ITodoService`），不持久化，重启丢失。

#### A6. Approval 模型严重不匹配

前端 Approval 模型：

```typescript
interface PendingApproval {
  id, toolName, toolIcon, agentName, sessionId, sessionName,
  parameters, requestedAt, waitingMinutes
}
interface ApprovalHistory {
  id, toolName, sessionId, decision, decidedBy, decidedAt, responseTime
}
interface ApprovalRule {
  id, toolName, ruleType('always-allow'|'always-deny'), createdBy, createdAt
}
```

后端仅有 `ToolInvocationEntity.ApprovalStatus`（字段级别），没有独立的 Approval 实体，也没有 ApprovalRule 概念。

**建议**: 新增 `ApprovalRuleEntity` 表，并在 `ToolInvocationEntity` 上扩展审批相关信息。

### B. 前端功能无后端支持

#### B1. Dashboard 统计数据（完全缺失）

前端 Dashboard 展示以下统计数据，但后端没有任何聚合 API：

- 今日会话总数、自动解决率、平均处理时间、待审批数
- 告警类型分布（柱状图）
- Top 服务排行

#### B2. Session 列表 API（完全缺失）

前端 Sessions 页面需要：

- 分页查询会话列表
- 按 Status/Source 筛选
- 按 alertName/sessionId/service 搜索
- 排序

当前后端只有 `GET /api/session/{id}` 单条查询。

#### B3. Session 时间线/对话流（缺失）

前端 SessionDetail 页面最核心的功能是一个**统一的对话流**，混合展示：

- 用户消息 / Agent 消息 / 系统消息（来自 `MessageEntity`）
- Agent 启动事件（来自 `AgentRunEntity`）
- 工具调用及结果（来自 `ToolInvocationEntity`）
- 思考过程 / 诊断更新

后端有这些数据但分散在不同表中，没有统一的时间线 API。

#### B4. Session 内对话（部分支持）

前端有聊天输入框，可以在 Session 运行期间向 Agent 发消息。后端 `POST /api/sre/chat` 支持通过 `SessionId` 继续对话，但 API 设计可能需要调整以更好匹配前端场景（直接在 Session 详情页发送消息）。

#### B5. 工具调用列表 API（缺失）

前端 SessionDetail 右侧面板展示该 Session 的所有工具调用，需要 `GET /api/sessions/{id}/tool-invocations`。

#### B6. Todo 列表 API（缺失）

前端 SessionDetail 右侧面板展示 Agent 的 Todo 列表，需要 `GET /api/sessions/{id}/todos`。

#### B7. 诊断详情 API（缺失）

前端展示结构化诊断信息，需要 `GET /api/sessions/{id}/diagnosis`。

#### B8. 审批管理系统（大量缺失）

前端 Approvals 页面功能：

- 待审批列表（含工具参数预览）
- 审批/拒绝操作（含"永久"选项）
- 审批历史查询
- 永久审批规则的 CRUD

后端仅有基础的 `ApprovalStatus` 字段，没有独立的审批流程。

#### B9. 工具和 Agent 注册表（缺失）

前端 Tools 和 Agents 页面展示：

- 已注册工具列表 + 调用统计（次数、成功率、平均耗时）
- 工具启用/禁用开关
- Agent 列表 + 性能统计
- Agent 启用/禁用开关

后端工具和 Agent 都是代码中注册的，没有注册表 API，也没有统计接口。

#### B10. Settings 全面缺失

前端 Settings 页面包含 7 个配置子页面，后端没有任何配置管理 API：

- 通用设置、LLM 配置、Slack 集成、告警解析器、审批设置、可观测性、数据库状态

#### B11. 通知系统（缺失）

前端有实时通知下拉菜单，展示告警、审批请求、会话完成等通知。后端没有通知 API。

#### B12. 全局搜索（缺失）

前端 CommandPalette 支持跨 Session/Tool/Agent 搜索，后端没有搜索 API。

#### B13. 实时更新（SSE 未暴露）

前端 `ConnectionStatus` 组件假设有 WebSocket/SSE 连接。后端架构设计了 SSE（`ISseWriter`），但没有暴露为公开端点供前端订阅。

#### B14. 诊断反馈（缺失）

前端在 Session 完成后显示"诊断正确/不正确/部分正确"反馈按钮，后端没有反馈 API。

### C. 后端关键数据无法在前端展示

#### C1. Checkpoint 数据

后端有完整的 Checkpoint 系统（断点恢复），但前端未设计任何展示。

#### C2. DiagnosticData

后端有 `DiagnosticDataEntity` 存储日志/指标原始数据（含 TTL），前端未设计直接展示入口。

#### C3. Token 使用量

后端 `MessageEntity.EstimatedTokens` 追踪 Token 使用。前端 SessionDetail 有一个小的 Tooltip 展示 Token 使用，但数据来源是硬编码的。

#### C4. Agent 执行细节

后端 `AgentRunEntity` 记录了每次 Agent 执行的详细信息（输入/输出/置信度/Finding），前端时间线展示了部分但不完整。

---

## 四、建议实现的 API 列表

### 第一阶段：核心 CRUD（支撑 Sessions 和 SessionDetail 页面）


| #   | 方法   | 路由                                    | 说明                                                 | 优先级 |
| --- | ---- | ------------------------------------- | -------------------------------------------------- | --- |
| 1   | GET  | `/api/sessions`                       | 会话列表，支持分页/筛选/排序/搜索                                 | P0  |
| 2   | GET  | `/api/sessions/{id}`                  | 会话详情（扩展现有，补充 source/severity/duration/steps）       | P0  |
| 3   | GET  | `/api/sessions/{id}/timeline`         | 统一时间线（合并 messages + agent_runs + tool_invocations） | P0  |
| 4   | GET  | `/api/sessions/{id}/diagnosis`        | 结构化诊断信息                                            | P0  |
| 5   | GET  | `/api/sessions/{id}/tool-invocations` | 工具调用列表                                             | P0  |
| 6   | GET  | `/api/sessions/{id}/todos`            | Todo 列表                                            | P0  |
| 7   | POST | `/api/sessions/{id}/messages`         | 向运行中的 Session 发送消息                                 | P0  |
| 8   | POST | `/api/sessions/{id}/feedback`         | 提交诊断反馈                                             | P1  |


### 第二阶段：Dashboard 和实时更新


| #   | 方法  | 路由                                  | 说明                     | 优先级 |
| --- | --- | ----------------------------------- | ---------------------- | --- |
| 9   | GET | `/api/dashboard/stats`              | 统计概览（总数、解决率、平均时间、待审批数） | P0  |
| 10  | GET | `/api/dashboard/activities`         | 跨 Session 实时活动流        | P1  |
| 11  | GET | `/api/dashboard/active-sessions`    | 当前活跃会话（含进度）            | P0  |
| 12  | GET | `/api/dashboard/alert-distribution` | 告警类型分布（图表数据）           | P2  |
| 13  | GET | `/api/dashboard/top-services`       | Top 服务排行               | P2  |
| 14  | GET | `/api/events/stream`                | SSE 实时事件流              | P1  |


### 第三阶段：审批管理


| #   | 方法     | 路由                            | 说明       | 优先级 |
| --- | ------ | ----------------------------- | -------- | --- |
| 15  | GET    | `/api/approvals/pending`      | 待审批列表    | P0  |
| 16  | POST   | `/api/approvals/{id}/approve` | 批准工具执行   | P0  |
| 17  | POST   | `/api/approvals/{id}/reject`  | 拒绝工具执行   | P0  |
| 18  | GET    | `/api/approvals/history`      | 审批历史     | P1  |
| 19  | GET    | `/api/approvals/rules`        | 永久审批规则列表 | P1  |
| 20  | POST   | `/api/approvals/rules`        | 创建审批规则   | P1  |
| 21  | DELETE | `/api/approvals/rules/{id}`   | 删除审批规则   | P1  |


### 第四阶段：注册表和配置


| #   | 方法  | 路由                         | 说明              | 优先级 |
| --- | --- | -------------------------- | --------------- | --- |
| 22  | GET | `/api/tools`               | 工具注册表 + 统计      | P1  |
| 23  | PUT | `/api/tools/{name}/config` | 工具配置（启用/禁用）     | P2  |
| 24  | GET | `/api/agents`              | Agent 注册表 + 统计  | P1  |
| 25  | PUT | `/api/agents/{id}/config`  | Agent 配置（启用/禁用） | P2  |
| 26  | GET | `/api/settings`            | 获取系统配置          | P2  |
| 27  | PUT | `/api/settings`            | 更新系统配置          | P2  |
| 28  | GET | `/api/search`              | 全局搜索            | P2  |


---

## 五、前端项目实施方案

### 5.1 技术选型

基于 Figma Make 已生成的代码，前端技术栈为：

- **React 19** + **React Router 7**
- **Vite** 构建
- **Tailwind CSS 4** + **shadcn/ui** 组件库
- **Recharts** 图表
- **Lucide Icons**

### 5.2 项目结构

```
frontend/
├── src/
│   ├── app/
│   │   ├── components/        # 通用业务组件
│   │   │   ├── ui/            # shadcn/ui 基础组件（从 Figma Make 导出）
│   │   │   ├── CommandPalette.tsx
│   │   │   ├── NotificationDropdown.tsx
│   │   │   └── ConnectionStatus.tsx
│   │   ├── layout/
│   │   │   └── AppLayout.tsx
│   │   ├── pages/             # 页面组件
│   │   │   ├── Dashboard.tsx
│   │   │   ├── Sessions.tsx
│   │   │   ├── SessionDetail.tsx
│   │   │   ├── Approvals.tsx
│   │   │   ├── Tools.tsx
│   │   │   ├── Agents.tsx
│   │   │   └── Settings.tsx
│   │   ├── lib/
│   │   │   ├── types.ts       # 类型定义（需对齐后端）
│   │   │   ├── api.ts         # API 客户端
│   │   │   └── hooks/         # 自定义 hooks
│   │   │       ├── useSession.ts
│   │   │       ├── useSessions.ts
│   │   │       ├── useDashboard.ts
│   │   │       ├── useApprovals.ts
│   │   │       ├── useSSE.ts
│   │   │       └── ...
│   │   ├── routes.ts
│   │   └── App.tsx
│   └── styles/
├── package.json
└── vite.config.ts
```

### 5.3 实施阶段（按 Figma 基线调整）

**Phase 0: Figma 设计上下文冻结（0.5 天，已完成）**

- 解析 Figma Make URL 并锁定 fileKey：`ZhSl72jDQYMWwYCa02IApx`
- 获取 Figma MCP 资源索引并记录关键源文件
- 将 Figma URL 写入计划与执行看板，作为前端开发基准

**Phase 1: 基础框架搭建（1-2 天，进行中）**

- 以 Figma 的路由 IA 建立前端骨架（Dashboard/Sessions/SessionDetail/Approvals/Tools/Agents/Settings）
- 建立 API 客户端层（`lib/api.ts`）-- 使用 fetch + 类型安全的封装
- 引入 TanStack Query 并建立 `useSessions` 等 hook 骨架
- 对齐前后端类型定义（特别是 Session、Status 枚举）

**本轮已完成调整（实现对齐 Figma）**

- 前端由单页占位切换为多路由骨架，路由结构与 Figma `routes.ts` 一致
- 新增 `AppLayout` 侧栏导航，覆盖 7 个主页面入口
- `Sessions` 页已接入后端 `GET /api/sessions`（含 status/source/search 参数）
- 新增前端 QA 报告：`docs/qa/qa-report-frontend-scaffold.md`

**Phase 2: 核心页面联调（3-5 天，依赖后端 P0 API）**

- Sessions 列表页 -- 对接 `GET /api/sessions`
- SessionDetail 页 -- 对接 timeline/diagnosis/tools/todos API
- 实现 SSE 订阅 hook（`useSSE`），驱动实时更新

**Phase 3: Dashboard 和审批（2-3 天）**

- Dashboard 统计和图表 -- 对接 dashboard API
- Approvals 页面 -- 对接审批 API

**Phase 4: 注册表和设置（2-3 天）**

- Tools/Agents 页面 -- 对接注册表 API
- Settings 页面 -- 对接配置 API

### 5.4 关键技术决策

- **数据获取**: 建议使用 **TanStack Query (React Query)** 管理服务端状态，自带缓存/重试/轮询
- **实时更新**: 使用 SSE（`EventSource`）替代 WebSocket，与后端 SSE 设计一致
- **状态管理**: 页面级状态用 React 自带 `useState`/`useReducer`；跨页面共享（如通知数、连接状态）用 React Context 或 Zustand
- **API 类型安全**: 定义共享的 TypeScript 类型，与后端 DTO 保持一致

### 5.5 需要后端配合的前置修改

1. `**SessionEntity` 增加字段**: `AlertSource`(string), `AlertSeverity`(string)
2. **定义 Diagnosis JSON Schema**: `{ hypothesis, confidence, evidence[], recommendedActions[] }`
3. **新增 `ApprovalRuleEntity`**: `Id, ToolName, RuleType, CreatedBy, CreatedAt`
4. **Todo 持久化**: 将 `ITodoService` 从内存实现改为数据库实现
5. **暴露 SSE 端点**: 将现有的 `ISseWriter` 集成到一个公开的 SSE 端点

