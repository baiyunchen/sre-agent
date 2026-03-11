---
name: git-small-step-commit
description: Create small-step git commits after each minimal verifiable code change with consistent message format and evidence updates. Use during active development of user stories.
---

# Git Small-Step Commit

## When to use

Use after each minimal verifiable change (a small, testable sub-task) during implementation.

## Workflow

1. Confirm changed files belong to one clear intent
2. Run minimal verification tests for this change
3. Stage only relevant files
4. Commit with story-aware message
5. Update `docs/13-大型变更任务看板.md` with commit hash and mapping

## Commit message format

`<type>(<scope>): <story-id> <summary>`

Examples:

- `feat(api): US-012 add alert filter endpoint`
- `fix(frontend): US-012 handle empty-state rendering`
- `test(backend): US-012 add analyze request validator tests`

## Safety checks

- Never commit secrets/credentials/env files
- Never mix unrelated changes in one commit
- If verification fails, do not mark the commit as done-state evidence
