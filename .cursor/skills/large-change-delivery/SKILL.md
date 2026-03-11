---
name: large-change-delivery
description: Drive large feature delivery with task slicing, user stories, contract-first RESTful APIs, mandatory testing/coverage gates, and end-to-end verification. Use when implementing cross-module features, large refactors, or any change requiring coordinated backend/frontend updates.
---

# Large Change Delivery

## Team orchestration

Use role agents in this order:

1. `project-manager` (progress and coordination)
2. `product-manager` (requirements and user stories)
3. `solution-architect` (decomposition and API contract)
4. `developer` (implementation and tests)
5. `qa-engineer` (e2e validation and sign-off)

Developer should apply:

- `git-small-step-commit`

## Step 1: Create/refresh task board

Before coding, update `docs/13-大型变更任务看板.md`:

1. Split work into small tasks (target half-day granularity)
2. Keep one main task in `in_progress`
3. Write each task as a User Story with acceptance criteria

## Step 2: Contract-first API design

If API is involved:

1. Update `docs/api/contracts/openapi.yaml` first
2. Ensure RESTful resource naming + method semantics
3. Align response states with frontend design (success/empty/error)

## Step 3: Implement with quality gates

Each task must include:

- Code implementation
- Unit tests
- Integration tests
- Small-step git commits after each minimal verifiable change

Coverage gate:

- Changed modules >= 85%
- Do not reduce overall project coverage
- If baseline is below 85%, add a coverage补齐 task immediately

## Step 4: Mandatory E2E before done

Before marking task `done`:

- Backend: run `qa-backend-e2e` (prefer `e2e-test` agent)
- Frontend: run `qa-frontend-e2e` browser checks

## Step 5: Record evidence

Update `docs/13-大型变更任务看板.md` with:

- Implementation summary
- Test commands and results
- Coverage numbers
- Commit records mapped to story/sub-task
- E2E outcomes and blockers
