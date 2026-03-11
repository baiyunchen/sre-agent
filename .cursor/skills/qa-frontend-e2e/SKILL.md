---
name: qa-frontend-e2e
description: Run frontend browser-based end-to-end QA on key user journeys, interaction states, and regressions. Use before completing stories with frontend impact.
---

# QA Frontend E2E

## Goal

Validate user-facing behavior in a real browser, including:

- Happy path
- Empty state
- Error state
- Key regression paths

## Workflow

1. Build a test checklist from story acceptance criteria
2. Open browser and execute critical user journeys
3. Validate API-related UI states (loading/success/empty/error)
4. Validate interaction details from design requirements
5. Re-run smoke checks for affected adjacent modules
6. Produce QA report and release recommendation

## Test checklist template

- [ ] Entry route loads successfully
- [ ] Core action flow succeeds end-to-end
- [ ] Empty-state copy and behavior are correct
- [ ] Error-state feedback is actionable
- [ ] Form validation and edge cases behave correctly
- [ ] No obvious console/runtime errors

## Required report fields

- Page/flow name
- Preconditions
- Steps and observed results
- Expected vs actual
- Evidence and defect notes
- Final verdict (Pass/Fail)
