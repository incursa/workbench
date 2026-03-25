---
artifact_id: ARC-WB-0001
artifact_type: architecture
title: "CLI onboarding flow with init + run wizard"
domain: WB
status: approved
owner: platform
satisfies:
  - REQ-CLI-0001
  - REQ-CLI-0002
  - REQ-CLI-0003
  - REQ-CLI-0004
  - REQ-CLI-0005
  - REQ-CLI-DOCTOR-0001
  - REQ-CLI-DOCTOR-0002
  - REQ-CLI-DOCTOR-0003
  - REQ-CLI-DOCTOR-0004
  - REQ-CLI-DOCTOR-0005
  - REQ-CLI-DOCTOR-0006
  - REQ-CLI-GUIDE-0001
  - REQ-CLI-GUIDE-0002
  - REQ-CLI-GUIDE-0003
  - REQ-CLI-GUIDE-0004
  - REQ-CLI-GUIDE-0005
  - REQ-CLI-INIT-0001
  - REQ-CLI-INIT-0002
  - REQ-CLI-INIT-0003
  - REQ-CLI-INIT-0004
  - REQ-CLI-INIT-0005
  - REQ-CLI-INIT-0006
related_artifacts:
  - SPEC-CLI-ONBOARDING
  - SPEC-CLI-DOCTOR
  - SPEC-CLI-GUIDE
  - SPEC-CLI-INIT
  - WI-WB-0001
  - VER-WB-0001
---

# ARC-WB-0001 - CLI onboarding flow with init + run wizard

## Purpose

We need a clearer first-run experience for the Workbench CLI. The current
command list and `init` behavior leave new users unsure of what to do next, and
setup steps are skipped when the repo appears partially configured.

## Requirements Satisfied

- [`REQ-CLI-0001`](../../requirements/CLI/SPEC-CLI-ONBOARDING.md)
- [`REQ-CLI-0002`](../../requirements/CLI/SPEC-CLI-ONBOARDING.md)
- [`REQ-CLI-0003`](../../requirements/CLI/SPEC-CLI-ONBOARDING.md)
- [`REQ-CLI-0004`](../../requirements/CLI/SPEC-CLI-ONBOARDING.md)
- [`REQ-CLI-0005`](../../requirements/CLI/SPEC-CLI-ONBOARDING.md)
- [`REQ-CLI-DOCTOR-0001`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-DOCTOR-0002`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-DOCTOR-0003`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-DOCTOR-0004`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-DOCTOR-0005`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-DOCTOR-0006`](../../requirements/CLI/SPEC-CLI-DOCTOR.md)
- [`REQ-CLI-GUIDE-0001`](../../requirements/CLI/SPEC-CLI-GUIDE.md)
- [`REQ-CLI-GUIDE-0002`](../../requirements/CLI/SPEC-CLI-GUIDE.md)
- [`REQ-CLI-GUIDE-0003`](../../requirements/CLI/SPEC-CLI-GUIDE.md)
- [`REQ-CLI-GUIDE-0004`](../../requirements/CLI/SPEC-CLI-GUIDE.md)
- [`REQ-CLI-GUIDE-0005`](../../requirements/CLI/SPEC-CLI-GUIDE.md)
- [`REQ-CLI-INIT-0001`](../../requirements/CLI/SPEC-CLI-INIT.md)
- [`REQ-CLI-INIT-0002`](../../requirements/CLI/SPEC-CLI-INIT.md)
- [`REQ-CLI-INIT-0003`](../../requirements/CLI/SPEC-CLI-INIT.md)
- [`REQ-CLI-INIT-0004`](../../requirements/CLI/SPEC-CLI-INIT.md)
- [`REQ-CLI-INIT-0005`](../../requirements/CLI/SPEC-CLI-INIT.md)
- [`REQ-CLI-INIT-0006`](../../requirements/CLI/SPEC-CLI-INIT.md)

## Design Summary

- Define "first run" as missing `.workbench/` or `.workbench/config`.
- On first run, execute `init` and then launch the `run` wizard.
- Keep `run` as the wizard entrypoint to guide users through common actions.
- Provide `--skip-wizard` for `init` to avoid the wizard in automation.

## Key Components

- First-run detection based on `.workbench/` presence
- Guided `init` path for bootstrap and configuration
- Guided `guide`/wizard entrypoint for common actions
- Repository health checks surfaced by `doctor`

## Data and State Considerations

The first-run state depends on local repository configuration and the presence of the Workbench workspace directory. Help output and command routing must remain stable after initialization.

## Edge Cases and Constraints

- Automation needs a skip path for the guided wizard.
- Standard help behavior should resume after initialization.
- The onboarding flow must not hide the normal command tree.

## Alternatives Considered

- Only update help output without a guided flow.
- Use an interactive `init` without launching a wizard afterward.
- Use a different command name (`wizard`, `assist`, `guide`) instead of `run`.

## Risks

- New users get a guided path without reading docs first.
- Automation remains viable through `--skip-wizard` and non-interactive flags.
- We must maintain a clear definition of "first run" across tooling.

## Open Questions

- None.
