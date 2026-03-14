# Stage Gate - hitl

- Feature: Human-in-the-Loop 完善
- PM Owner: project-manager
- Last Updated: 2026-03-14

## Gate Matrix

| Stage | Required Artifacts | Review Result | Decision | Notes |
|---|---|---|---|---|
| Product | `docs/product/prd-hitl.md` + User Stories/AC | pass | GO | US-H01~US-H04，范围清晰 |
| Designer | `docs/design/design-hitl.md`（UI 变更） | pass | GO | SessionDetail Interrupt/Resume/Approval UI 描述完整 |
| Architect | `docs/architecture/arch-hitl.md` + `docs/api/contracts/openapi.yaml` | pass | GO | per-session SSE、interrupt、resume、tool approval 契约已更新 |
| Developer | Code + Unit/Integration + Coverage + Small-step commits | pass | GO | 96/96 tests pass，5 commits，变更模块覆盖率 >=85% |
| Code Review | `docs/qa/code-review-hitl-phase1-4.md` | pass (after fix) | GO | 初次 Request Changes（2 Critical + 2 Major），修复后 Approved（commit `8fec783`） |
| QA | `docs/qa/qa-report-hitl-e2e.md` + backend e2e | pass | GO | 4/5 场景 PASS，1 场景 PARTIAL（环境依赖） |

## NO-GO Remediation

- Blocking issues: 无
- Required actions: 无
- Re-review condition: N/A

## Final Decision

- Release decision: **GO** — 所有门禁通过
- Residual risks:
  1. 工具审批触发场景依赖 agent 实际调用特定工具，需真实 AWS 环境验证完整链路
  2. 前端构建存在既有 TS 错误（非 HITL 引入），需后续独立修复
  3. 审批超时为 30 分钟固定值，后续可改为可配置
