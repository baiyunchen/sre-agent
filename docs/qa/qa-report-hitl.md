# QA Report - HITL (Human-in-the-Loop)

- Feature: Human-in-the-Loop 完善
- QA Date: 2026-03-14
- QA Owner: qa-engineer

## 测试范围

| Story | 描述 | 测试类型 |
|---|---|---|
| US-H01 | 后台执行 + Per-Session SSE | Backend E2E |
| US-H02 | 中断机制 | Backend E2E |
| US-H03 | 恢复与人类输入 | Backend E2E |
| US-H04 | 工具审批流程 | Backend E2E |

## 单元测试 & 集成测试

```
dotnet test sre-agent.sln
```

| 测试套件 | 通过 | 失败 | 跳过 |
|---|---|---|---|
| SreAgent.Framework.Tests | 25 | 0 | 0 |
| SreAgent.Application.Tests | 20 | 0 | 0 |
| SreAgent.Api.Tests | 51 | 0 | 0 |
| **合计** | **96** | **0** | **0** |

## E2E 测试结果

### 场景 1: 后台执行 + SSE 实时推送 — PASS

| 步骤 | 预期 | 实际 | 状态 |
|---|---|---|---|
| POST /api/sre/analyze | 202 + sessionId | 202，sessionId 返回 | PASS |
| GET /api/sessions/{id}/stream | SSE 连接建立 | 连接成功 | PASS |
| 等待 session 完成 | 收到 session.ended 事件 | 90s 内收到 | PASS |
| GET /api/sessions/{id} | 返回会话详情 | 200，包含完整字段 | PASS |

### 场景 2: 中断机制 — PASS

| 步骤 | 预期 | 实际 | 状态 |
|---|---|---|---|
| POST /api/sre/analyze | 创建分析任务 | 202 | PASS |
| POST /api/sessions/{id}/interrupt | 200 | 200，message="Session interrupted" | PASS |
| GET /api/sessions/{id} | status=Interrupted | status=Interrupted | PASS |

### 场景 3: 恢复与人类输入 — PASS

| 步骤 | 预期 | 实际 | 状态 |
|---|---|---|---|
| POST /api/sessions/{id}/resume + continueInput | 202 | 202 | PASS |
| GET /api/sessions/{id} | status=Running | status=Running | PASS |

### 场景 4: 工具审批规则 — PARTIAL

| 步骤 | 预期 | 实际 | 状态 |
|---|---|---|---|
| POST /api/approvals/rules (require-approval) | 201 | 201，规则创建成功 | PASS |
| POST /api/sre/analyze | 创建分析任务 | 202 | PASS |
| SSE 等待 tool.approval_required | 90s 内收到 | 超时未收到 | PARTIAL |
| POST approve | 200 | 未执行 | SKIP |

**原因**: Agent 在测试环境中未在 90s 内调用 `cloudwatch_simple_query` 工具（该工具依赖真实 AWS 凭证）。审批规则创建、审批 API 本身验证通过。

### 场景 5: API 健壮性 — PASS

| 测试项 | 预期 | 实际 | 状态 |
|---|---|---|---|
| 不存在的 session 调用 interrupt | 400 | 400 | PASS |
| 已完成的 session 调用 interrupt | 400 | 400 | PASS |
| 不存在的 tool invocation 调用 approve | 404 | 404 | PASS |

## Code Review

- 报告: `docs/qa/code-review-hitl-phase1-4.md`
- 初次结论: Request Changes（2 Critical + 2 Major）
- 修复后结论: Approved
- 修复提交: `8fec783`

## 总结

| 类别 | 状态 |
|---|---|
| 单元测试 | 96/96 PASS |
| 集成测试 | 含在上述 96 个用例中 |
| E2E 场景 1（后台执行+SSE） | PASS |
| E2E 场景 2（中断） | PASS |
| E2E 场景 3（恢复） | PASS |
| E2E 场景 4（工具审批） | PARTIAL（环境依赖） |
| E2E 场景 5（API 健壮性） | PASS |
| Code Review | PASS（修复后） |

## 结论

**PASS** — 核心功能（后台执行、SSE、中断、恢复）全部通过。工具审批的 API 和规则管理验证通过，完整的审批触发链路需要配置真实 AWS 环境后补充验证。

## 残余风险

1. 工具审批完整链路需真实 AWS 环境验证
2. 前端构建存在既有 TS 错误（非 HITL 引入）
3. 审批超时为固定 30 分钟，后续可配置化
