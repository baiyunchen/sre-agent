# Stage Gate - hitl

- Feature: Human-in-the-Loop 完善
- PM Owner: project-manager
- Last Updated: 2026-03-14

## Gate Matrix

| Stage | Required Artifacts | Review Result | Decision | Notes |
|---|---|---|---|---|
| Product | `docs/product/prd-hitl.md` + User Stories/AC | pass | GO | US-H01~US-H04，范围清晰 |
| Architect | `docs/architecture/arch-hitl.md` + `docs/api/contracts/openapi.yaml` | pass | GO | per-session SSE、interrupt、resume、tool approval 契约已更新 |
| Designer | `docs/design/design-hitl.md`（UI 变更） | pass | GO | SessionDetail Interrupt/Resume/Approval UI 描述完整 |
| Developer | Code + Unit/Integration + Coverage + Small-step commits | pending | - | Phase 1 (US-H01) → Phase 2 (US-H02) 优先 |
| QA | `docs/qa/qa-report-hitl.md` + backend/frontend e2e | pending | - | |

## NO-GO Remediation

- Blocking issues: (none yet)
- Required actions: -
- Re-review condition: -

## Final Decision

- Release decision: pending
- Residual risks: -
