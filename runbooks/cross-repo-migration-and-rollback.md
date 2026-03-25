# Runbook: Cross-Repo Migration and Rollback (coherent-v1)

## Purpose

Migrate an existing repository to the canonical Spec Trace model with a safe,
repeatable process and a clean rollback path.

## Scope

Use for repositories that are adopting canonical work items, architecture,
verification, and derived navigation outputs.

## Preconditions

- Repository is a git repo and you can create branches.
- Work is done from a clean working tree.
- Workbench CLI is available (`workbench --help`).
- If GitHub issue sync will be used, auth is configured (`GITHUB_TOKEN` or `gh auth`).

## Required access/tools

- Git push rights for a feature branch in the target repository.
- `workbench` CLI (or local `dotnet run --project ... --` equivalent).
- Optional: `gh` CLI for PR creation.

## Procedure

1. Create an isolated migration branch.
   - `git status --porcelain`
   - `git switch -c chore/workbench-migration-coherent-v1`
2. Capture baseline health checks.
   - `workbench doctor --json > artifacts/workbench-doctor.before.json`
   - `workbench validate --strict > artifacts/workbench-validate.before.txt`
3. Scaffold missing Workbench structure (safe to re-run).
   - `workbench scaffold`
   - `workbench config show --format json`
4. Dry-run synchronization and migration.
   - `workbench sync --dry-run --issues false`
   - `workbench migrate coherent-v1 --dry-run`
5. Apply migration changes.
   - `workbench sync --issues false`
   - `workbench migrate coherent-v1`
6. Rebuild navigation and derived outputs.
   - `workbench nav sync --issues false --force`
7. Validate post-migration health.
   - `workbench validate --strict`
   - `workbench doctor --json > artifacts/workbench-doctor.after.json`
   - Confirm `tracking/migration-coherent-v1-YYYY-MM-DD.md` was written.
8. Commit and open PR.
   - `git add docs .workbench artifacts`
   - `git commit -m "Migrate repository to Workbench coherent-v1"`
   - `git push -u origin chore/workbench-migration-coherent-v1`

## Validation

- `workbench validate --strict` exits `0`.
- `workbench doctor` reports checks without unhandled exceptions.
- Work items are under `specs/work-items` with coherent status placement.
- Docs index and derived navigation blocks are regenerated.
- Migration report exists in `tracking/`.

## Rollback / recovery

- Before merge: close the migration PR and delete the branch.
- After merge: revert the migration commit in a follow-up PR.
  - `git revert <migration-commit-sha>`
- If command failures occur mid-run, fix the root cause and re-run from step 4 on the same branch.

## Escalation / contacts

- Platform maintainers for Workbench CLI behavior/regressions.
- Repository owners for domain-specific schema/link exceptions.

## Related docs

- [`specs/generated/commands.md`](../specs/generated/commands.md)
- [`specs/requirements/_index.md`](../specs/requirements/_index.md)
- [`tracking/hardening-gate-2026-02-19.md`](../tracking/hardening-gate-2026-02-19.md)
- [`tracking/migration-coherent-v1-2026-02-19.md`](../tracking/migration-coherent-v1-2026-02-19.md)
