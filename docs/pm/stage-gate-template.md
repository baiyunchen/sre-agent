# Stage Gate - `<feature-key>`

- Feature: `<feature name>`
- PM Owner: `<name>`
- Last Updated: `<yyyy-mm-dd>`

## Gate Matrix

| Stage | Required Artifacts | Review Result | Decision | Notes |
|---|---|---|---|---|
| Product | `docs/product/prd-<feature-key>.md` + User Stories/AC | pass/fail | GO/NO-GO | |
| Architect | `docs/architecture/arch-<feature-key>.md` + `docs/api/contracts/openapi.yaml` | pass/fail | GO/NO-GO | |
| Developer | Code + Unit/Integration + Coverage + Small-step commits | pass/fail | GO/NO-GO | |
| QA | `docs/qa/qa-report-<feature-key>.md` + backend/frontend e2e | pass/fail | GO/NO-GO | |

## NO-GO Remediation

- Blocking issues:
  - `<issue>`
- Required actions:
  - `<action>`
- Re-review condition:
  - `<condition>`

## Final Decision

- Release decision: `GO/NO-GO`
- Residual risks:
  - `<risk>`
