---
name: qa-backend-e2e
description: Execute backend end-to-end QA for alert analysis pipeline. Use when validating alert trigger to analyze API full chain, especially before marking stories done.
---

# QA Backend E2E

## Goal

Validate the backend end-to-end chain:

`trigger alert -> webhook receives event -> call /api/sre/analyze -> verify analysis result`

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
