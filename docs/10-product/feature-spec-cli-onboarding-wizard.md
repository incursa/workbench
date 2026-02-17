---
workbench:
  type: spec
  workItems:
    - TASK-0001
  codeRefs: []
  pathHistory: []
  path: /docs/10-product/feature-spec-cli-onboarding-wizard.md
owner: platform
status: draft
updated: 2025-12-27
---

# Feature Spec: CLI Onboarding, Init Walkthrough, and Wizard Mode

## Summary

Improve first-run and ongoing CLI clarity by tightening help output, making
`init` a guided setup instead of a skip-only check, and adding a wizard/REPL
command that walks users through common actions.

## Goals

- Make the default `workbench` help output self-explanatory for new users.
- Ensure `init` guides setup even when parts of the repo already exist.
- Provide an interactive wizard that helps users choose and create docs/items.
- Eliminate duplicate or confusing version reporting.

## Non-goals

- Redesign every CLI command or flag in this release.
- Replace existing subcommands or change file formats.
- Introduce a graphical UI.

## User stories / scenarios

- As a new user, I can run `workbench` and immediately understand which
  commands are leaf actions vs. groups of related commands.
- As a new user, I can run `workbench init` and be guided through setup even
  when some folders already exist.
- As a user who does not know which command to use, I can run a wizard and be
  walked to the right document or work item template.
- As a maintainer, I can get a concise human-readable doctor summary and
  optionally a JSON report.

## Requirements

- Default help output:
  - Remove the confusing duplication of `version` as both command and flag, or
    clearly disambiguate them.
  - Ensure the version value is populated from the build/assembly metadata.
  - Distinguish command groups vs. leaf commands with explicit labels or
    description hints (example: "group: manages work items").
- `doctor` command:
  - Show a human-readable summary with clear next steps.
  - Provide JSON output only when a flag is set (example: `--json`).
  - Include guidance for resolving failed checks.
- `init` command:
  - Run a guided walkthrough with step-by-step prompts.
  - If a step is already complete, show why and offer to review/repair it.
  - Prompt to add missing front matter where applicable.
  - Offer to configure OpenAI settings (and explain required values).
  - Ask where credentials should live: outside repo or in an ignored local file.
  - If a local file is chosen, add it to `.gitignore` when missing.
  - Allow non-interactive mode via flags for CI use.
  - Offer `--skip-wizard` to avoid launching the wizard after init.
- Wizard/REPL command:
  - Provide a command (`run`) that presents top-level actions.
  - Offer descriptions of each doc/work item type.
  - Guide the user through required fields and template selection.
  - Exit cleanly to standard commands with an explicit "next steps" message.
  - After `init`, automatically start the wizard unless `--skip-wizard` is set.

## UX notes

- Use clear, short prompts with defaults shown in brackets.
- Allow skipping any step with an explicit choice, not silent skip.
- Keep output minimal but informative; avoid dumping raw JSON by default.
- Provide a "what did we do" summary at the end of `init` and wizard runs.

## First run detection

- Treat absence of `.workbench/` or `.workbench/config` as "first run".
- If first run:
  - Start `init` automatically.
  - On completion, launch `run` unless `--skip-wizard` is set.
- If not first run:
  - `workbench` continues to show help output by default.
  - `init` remains available for reconfiguration or repairs.

## Dependencies

- `workbench` config schema and doc schema updates if new fields are needed.
- CLI command routing for interactive mode (likely shared helpers).
- Optional integration with OpenAI configuration flows and secrets storage.

## Risks and mitigations

- Risk: interactive prompts break automation.
  - Mitigation: add `--non-interactive` and explicit flags for each step.
- Risk: added UX complexity slows experienced users.
  - Mitigation: keep defaults fast and provide `--yes` or `--skip` options.
- Risk: users ignore help updates.
  - Mitigation: improve the first-run output and include short examples.

## Related work items

- [TASK-0001](/docs/70-work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)

## Related ADRs

- </docs/40-decisions/ADR-2025-12-27-cli-onboarding-wizard.md>

## Decisions

- Keep `run` as the wizard command name for now.
- `doctor` defaults to human-readable output; JSON requires `--json`.
- During `init`, prompt for credential storage location and add `.gitignore`
  entry for any local file option.
- First run is defined by missing `.workbench/config` (or `.workbench/`).
- On first run, run `init` and then drop into `run` automatically unless
  `--skip-wizard` is set.

## Open questions

- None.
