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

After each phase, `project-manager` must review artifacts and decide `GO/NO-GO`.

## Step 1: Create/refresh task board

Before coding, update `docs/13-大型变更任务看板.md`:

1. Split work into small tasks (target half-day granularity)
2. Keep one main task in `in_progress`
3. Write each task as a User Story with acceptance criteria
4. Create stage gate record: `docs/pm/stage-gate-<feature-key>.md`

## Step 1.5: Document artifacts per role (MANDATORY)

Each role MUST produce its artifact before the next phase starts. These are NOT optional:

- **Product**: `docs/product/prd-<feature-key>.md` — call `product-manager` to produce
- **Architecture**: `docs/architecture/arch-<feature-key>.md` — call `solution-architect` to produce
- **Stage gate**: `docs/pm/stage-gate-<feature-key>.md` — call `project-manager` to review each phase
- **QA**: `docs/qa/qa-report-<feature-key>.md` — call `qa-engineer` to produce
- Developer: doc optional, but task board evidence mandatory

After Product artifact is created, `project-manager` must review and mark Product stage `GO` in stage-gate.
After Architecture artifact is created, `project-manager` must review and mark Architect stage `GO` in stage-gate.

## Step 1.7: Pre-development gate check (BLOCKING)

Before ANY coding begins, verify all of the following. If any check fails, STOP and fix it first:

1. `docs/product/prd-<feature-key>.md` exists and is non-empty
2. `docs/architecture/arch-<feature-key>.md` exists and is non-empty
3. `docs/pm/stage-gate-<feature-key>.md` has Product = `GO` and Architect = `GO`

If checks fail: mark the story as `blocked` on the task board, state which artifact is missing, and hand off to the responsible role agent. Do NOT proceed to Step 2.

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
- PM stage-gate decisions (`GO/NO-GO`)
