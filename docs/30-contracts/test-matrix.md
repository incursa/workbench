---
workbench:
  type: contract
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/30-contracts/test-matrix.md
owner: platform
status: active
updated: 2026-02-19
---

# Test Matrix (CLI + Core)

This matrix maps spec-level behavior to executable test coverage for `Workbench.Cli` and `Workbench.Core`.

## Core Command Scenarios

| Area | Scenario | Input class | Expected behavior | Test |
| --- | --- | --- | --- | --- |
| Repo detection | Non-git path | invalid env | Exit `2`; friendly error text; no stack trace by default | `ResilienceTests.Doctor_NonGitRepo_ReturnsFriendlyErrorWithoutStackTrace` |
| Repo detection | Non-git path + JSON format | invalid env | JSON error envelope (`ok=false`, `error.code`, `error.message`, `error.hint`) | `ResilienceTests.Doctor_NonGitRepo_JsonFormatAfterCommand_ReturnsErrorEnvelope` |
| Repo detection | Non-git path + debug | invalid env | Exit `2`; exception details present in stderr | `ResilienceTests.Doctor_NonGitRepo_DebugIncludesExceptionDetails` |
| Bootstrap diagnostics | Git repo without scaffold | degraded env | `doctor --format json` returns warning checks (`config`, `paths`) and exit `1` | `ResilienceTests.Doctor_GitRepoWithoutScaffold_ReturnsWarningsInJson` |
| Config parsing | Malformed `.workbench/config.json` | invalid file content | Exit `2`; explicit config error; no stack trace | `ResilienceTests.ConfigShow_MalformedConfig_ReturnsConfigError` |
| Schema validation | Missing schema contracts | invalid env | `validate --strict` exits `2` and reports missing schema paths | `ResilienceTests.ValidateStrict_MissingSchemas_ReturnsErrors` |
| Global options | Options after subcommand | valid usage variation | `--repo` and `--format json` work after subcommands | `ResilienceTests.ItemList_GlobalOptionsAfterSubcommand_AreAccepted` |
| Migration | Move done/dropped to done dir | valid mutation | item file relocated to `docs/70-work/done` | `MigrationCommandTests.MigrateCoherentV1_MovesTerminalItemsToDoneDirectory` |
| Migration | Dry-run does not mutate | valid dry-run | reports moves without file mutation; no report file | `MigrationCommandTests.MigrateCoherentV1_DryRunReportsWithoutMovingFiles` |
| Migration | Move active item from done to items | repair mutation | item with non-terminal status moves back to `docs/70-work/items` | `MigrationCommandTests.MigrateCoherentV1_MovesActiveItemsBackToItemsDirectory` |

## Core File Contract Scenarios

| Contract target | Scenario | Expected behavior | Test |
| --- | --- | --- | --- |
| Front matter parser | Parse+serialize round trip | body preserved and key fields retained | `FrontMatterTests.ParseAndSerialize_RoundTripsBody` |
| Config schema | Invalid config schema payload | validation returns schema errors | `SchemaValidationTests.ValidateConfig_ReturnsSchemaErrors` |
| Work-item schema | Invalid work-item front matter payload | validation returns schema errors | `SchemaValidationTests.ValidateFrontMatter_ReturnsSchemaErrors` |
| Repo validation | Broken local markdown links | validation errors include missing link target | `ValidationTests.ValidateRepo_FindsBrokenMarkdownLinks` |
| Work-item location rule | Done item in active directory | validation error for terminal-status placement | `ValidationTests.ValidateRepo_FailsWhenDoneItemLivesInActiveDirectory` |
| Work-item location rule | Active item in done directory | validation error for non-terminal placement | `ValidationTests.ValidateRepo_FailsWhenActiveItemLivesInDoneDirectory` |

## Coverage Gate

Coverage gate values and critical file list are defined in:

- `docs/30-contracts/test-gate.contract.yaml`
- `scripts/testing/verify-critical-coverage.ps1`
