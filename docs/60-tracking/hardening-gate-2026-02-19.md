---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/60-tracking/hardening-gate-2026-02-19.md
owner: platform
status: active
updated: 2026-02-19
---

# Hardening Gate (2026-02-19)

This checklist is the release bar before upgrading other repositories to this model.

## Completed in this pass

- [x] Replace raw exception dumps with centralized error reporting.
- [x] Add structured JSON error envelope for failures (`ok: false`, `error.code`, `error.message`, `error.hint`).
- [x] Add debug-only exception diagnostics via `--debug` / `WORKBENCH_DEBUG=1`.
- [x] Make global options position-independent (`--repo`, `--format`, `--no-color`, `--quiet`, `--debug`).
- [x] Add integration tests for:
  - non-git repo friendly failure output (no stack trace),
  - JSON error envelope in non-git repo,
  - global options after subcommand.
- [x] Run integration tests in CI and quality gates (not only unit tests).

## Remaining before broad rollout

- [ ] Extend CI-required smoke matrix for:
  - non-git folder,
  - git repo without scaffold,
  - malformed config.
- [ ] Replace or re-enable currently skipped integration scenarios so release gates are fully enforced.
- [ ] Run and document a two-repo external trial (one clean, one messy legacy) with zero unhandled exceptions.
- [ ] Publish final operator runbook for cross-repo migration and rollback.

## Current recommendation

- Pilot rollouts are safe.
- Broad rollout should wait until all remaining items above are complete.
