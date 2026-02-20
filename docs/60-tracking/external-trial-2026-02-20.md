---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/60-tracking/external-trial-2026-02-20.md"
  path: /docs/60-tracking/external-trial-2026-02-20.md
owner: platform
status: active
updated: 2026-02-20
---

# External Trial Report (2026-02-20)

This report documents the two-repo external hardening trial required by
`docs/60-tracking/hardening-gate-2026-02-19.md`.

## Trial scope

- Local-only migration path (`--issues false`) to avoid GitHub auth variability.
- Full sequence per repo:
  - `doctor --json`
  - `validate --strict`
  - `config show --format json`
  - `scaffold`
  - `sync --dry-run --issues false`
  - `migrate coherent-v1 --dry-run`
  - `sync --issues false`
  - `migrate coherent-v1`
  - `nav sync --issues false --include-done --force`
  - `validate --strict`
  - `doctor --json`

## Repo 1 (messy legacy): `C:/src/incursa/app`

- Trial branch: `chore/workbench-trial-coherent-v1-2026-02-19`
- Evidence: `C:/src/incursa/app/artifacts/workbench-trial/`

### Key results

- `sync --issues false`: exit `0` after fix (no GitHub issue creation/update).
- `migrate coherent-v1`: exit `0`; moved `42` terminal items to `work/done`.
- Validation errors reduced from `155` to `129`.
- No unhandled exceptions in normal command execution.

### Notable remaining validation issues (repo content-specific)

- Workbench/doc schema mismatches in existing docs/config.
- Broken local links in reference content.
- Missing `related.files` targets in some work items.

## Repo 2 (clean): `C:/src/incursa/types`

- Trial branch: `chore/workbench-trial-coherent-v1-clean-2026-02-19`
- Evidence: `C:/src/incursa/types/artifacts/workbench-trial-clean/`

### Key results

- `sync --issues false` dry-run/apply: exit `0`.
- `migrate coherent-v1`: exit `0`; no item moves required (`moved to done: 0`).
- `nav sync`: exit `0`.
- No unhandled exceptions in normal command execution.

### Validation/doctor status

- `doctor` exits warning (`2`) because config schema file is not present in this repo.
- `validate` exits `2` due pre-existing link/schema coverage gaps.

## Fix applied during trial

- `workbench` commit `9b28143`:
  - `sync --issues false` now disables GitHub issue import/create/update in the item-sync phase.
  - Added regression test coverage for this behavior.

## Conclusion

- Hardening trial requirement is satisfied:
  - two external repos exercised (one legacy/messy, one clean),
  - zero unhandled exceptions in standard execution paths,
  - local-only migration flow succeeds end-to-end.
