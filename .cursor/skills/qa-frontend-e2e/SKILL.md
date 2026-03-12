---
name: qa-frontend-e2e
description: Run frontend browser-based end-to-end QA on key user journeys, interaction states, and regressions. This is the PRIMARY e2e method for any task that has frontend impact. When a feature has both API and frontend, this single browser-based test covers both layers — no separate API testing needed.
---

# QA Frontend E2E

## Goal

Validate user-facing behavior in a real browser. This is the **primary E2E method** for any task involving frontend changes. When a feature spans both backend API and frontend UI, a single browser-based test covers both layers — no separate API testing is needed.

## Key principles

1. **Browser is mandatory**: Always open a real browser (via `browser-use` subagent) — never substitute with curl or API-only testing for frontend tasks.
2. **Single-pass coverage**: When the user journey exercises the API, that constitutes sufficient API verification. Do not duplicate with separate curl/API calls.
3. **Real port verification**: Use the actual frontend dev server port (e.g., `localhost:5173`), verifying CORS and real-world conditions.

## Workflow

1. Build a test checklist from story acceptance criteria
2. Open browser at the real frontend URL and execute critical user journeys
3. Validate API-related UI states (loading/success/empty/error) — this implicitly verifies the API
4. Validate interaction details from design requirements
5. Re-run smoke checks for affected adjacent modules
6. Produce QA report and release recommendation
7. Save report to `docs/qa/qa-report-<feature-key>.md` and sync result to task board

## Test checklist template

- [ ] Entry route loads successfully (no CORS or network errors)
- [ ] Core action flow succeeds end-to-end (implicitly verifies API)
- [ ] Empty-state copy and behavior are correct
- [ ] Error-state feedback is actionable
- [ ] Form validation and edge cases behave correctly
- [ ] No obvious console/runtime errors

## Required report fields

- Page/flow name
- Preconditions (frontend URL, backend running, test data)
- Steps and observed results
- Expected vs actual
- Evidence and defect notes
- Final verdict (Pass/Fail)
