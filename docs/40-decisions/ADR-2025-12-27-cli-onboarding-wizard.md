---
workbench:
  type: adr
  workItems:
    - TASK-0001
  codeRefs: []
  pathHistory: []
  path: /docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md
owner: platform
status: accepted
updated: 2025-12-27
---

# ADR-2025-12-27: CLI onboarding flow with init + run wizard

- Status: accepted
- Date: 2025-12-27
- Owner: platform

## Context

We need a clearer first-run experience for the Workbench CLI. The current
command list and `init` behavior leave new users unsure of what to do next, and
setup steps are skipped when the repo appears partially configured.

## Decision

- Define "first run" as missing `.workbench/` or `.workbench/config`.
- On first run, execute `init` and then launch the `run` wizard.
- Keep `run` as the wizard entrypoint to guide users through common actions.
- Provide `--skip-wizard` for `init` to avoid the wizard in automation.

## Alternatives considered

- Only update help output without a guided flow.
- Use an interactive `init` without launching a wizard afterward.
- Use a different command name (`wizard`, `assist`, `guide`) instead of `run`.

## Consequences

- New users get a guided path without reading docs first.
- Automation remains viable through `--skip-wizard` and non-interactive flags.
- We must maintain a clear definition of "first run" across tooling.

## Related specs

- </docs/10-product/feature-spec-cli-onboarding-wizard.md>

## Related work items

- [TASK-0001](/docs/70-work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)
