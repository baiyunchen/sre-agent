---
name: qa-backend-e2e
description: Execute backend end-to-end QA for alert analysis pipeline. Use ONLY for pure backend tasks that have no frontend page coverage. When a feature has both API and frontend, use qa-frontend-e2e instead to avoid duplicate testing.
---

# QA Backend E2E

## Goal

Validate the backend end-to-end chain for **pure backend tasks only**:

`trigger alert -> webhook receives event -> call /api/sre/analyze -> verify analysis result`

## When to use

- The task is a **pure backend** change with no corresponding frontend page
- The API is **not covered** by any frontend user journey
- Examples: webhook receiver changes, analysis pipeline internals, background jobs

## When NOT to use

- The feature has a frontend page that exercises the API — use `qa-frontend-e2e` instead
- The API is already tested through browser E2E — do not duplicate with curl/API tests

## Workflow

1. Confirm test scope from acceptance criteria
2. Run pre-flight checks (service health, credentials, environment readiness)
3. Execute scenario triggers and wait for alarm signals
4. Extract webhook evidence and build analyze API payload
5. Call API and validate response fields + root-cause correctness
6. Collect logs and classify pass/fail

## Execution convention

- Prefer using `e2e-test` agent for full pipeline execution
- If only partial validation is needed, explicitly state skipped steps and risks
- Record each scenario result in `docs/qa/qa-report-<feature>.md`

## Required report fields

- Scenario name
- Trigger details
- Alarm/webhook evidence
- API response checks
- Root cause validation
- Final verdict (Pass/Fail) and risk notes
