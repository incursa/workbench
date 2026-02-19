---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/60-tracking/hardening-gate-2026-02-19.md"
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
- [x] Add contract docs for testing and failures:
  - `docs/30-contracts/test-gate.contract.yaml`
  - `docs/30-contracts/test-matrix.md`
  - `docs/30-contracts/error-codes.md`
- [x] Add critical-surface coverage verification script (`scripts/testing/verify-critical-coverage.ps1`).
- [x] Add targeted parser/schema fuzz tests for resilience in `tests/Workbench.Tests/ParserFuzzTests.cs`.
- [x] Add scheduled mutation workflow for critical core files (`.github/workflows/mutation-critical.yml`).
- [x] Extend CI-required smoke matrix for:
  - non-git folder,
  - git repo without scaffold,
  - malformed config.

## Remaining before broad rollout

- [x] Replace or re-enable currently skipped integration scenarios so release gates are fully enforced.
- [ ] Run and document a two-repo external trial (one clean, one messy legacy) with zero unhandled exceptions.
- [x] Publish final operator runbook for cross-repo migration and rollback.

## Current recommendation

- Pilot rollouts are safe.
- Broad rollout should proceed after the two-repo external trial completes without regressions.
