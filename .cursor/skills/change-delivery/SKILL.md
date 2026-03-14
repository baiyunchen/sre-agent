---
name: change-delivery
description: Drive feature delivery with task slicing, user stories, contract-first RESTful APIs, mandatory testing/coverage gates, and end-to-end verification. Use when implementing features, bug fixes, refactors, or any change requiring coordinated backend/frontend updates.
---

# Change Delivery

## Team orchestration

Use role agents in this order:

1. `project-manager` (progress and coordination)
2. `product-manager` (requirements and user stories)
3. `designer` (UI design descriptions using `frontend-design` skill)
4. `solution-architect` (decomposition and API contract)
5. `developer` (implementation and tests)
6. `code-reviewer` (code quality gate using `code-reviewer` skill)
7. `qa-engineer` (e2e validation and sign-off)

Developer should apply:

- `git-small-step-commit`

After each phase, `project-manager` must review artifacts and decide `GO/NO-GO`.

## Step 1: Create/refresh task board

Before coding, update `docs/13-任务看板.md`:

1. Split work into small tasks (target half-day granularity)
2. Keep one main task in `in_progress`
3. Write each task as a User Story with acceptance criteria
4. Create stage gate record: `docs/pm/stage-gate-<feature-key>.md`

## Step 1.5: Document artifacts per role

- Product artifact: `docs/product/prd-<feature-key>.md`
- Design artifact: `docs/design/design-<feature-key>.md`
- Architect artifact: `docs/architecture/arch-<feature-key>.md`
- QA artifact: `docs/qa/qa-report-<feature-key>.md`
- Developer: doc optional, but task board evidence mandatory

## Step 1.6: Frontend design

Use `designer` agent with `frontend-design` skill:

1. Review existing UI patterns and component conventions
2. Produce design descriptions covering key screens and interaction states
3. Define success, empty, loading, and error states
4. Reference existing components that should be reused
5. Save artifact as `docs/design/design-<feature-key>.md`

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

## Step 3.5: Code review gate

Use `code-reviewer` agent with `code-reviewer` skill:

1. Review all implementation changes for correctness, maintainability, and security
2. Verify adherence to approved architecture and API contracts
3. If requesting changes, developer must address findings before proceeding
4. Only approved code moves forward to QA

## Step 4: Mandatory E2E before done

Before marking task `done`:

- Backend: run `qa-backend-e2e` (prefer `e2e-test` agent)
- Frontend: run `qa-frontend-e2e` browser checks

## Step 5: Record evidence

Update `docs/13-任务看板.md` with:

- Implementation summary
- Test commands and results
- Coverage numbers
- Commit records mapped to story/sub-task
- E2E outcomes and blockers
- PM stage-gate decisions (`GO/NO-GO`)
