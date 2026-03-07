---
workbench:
  type: contract
  workItems:
    - TASK-0015
    - TASK-0016
    - TASK-0017
    - TASK-0018
    - TASK-0019
    - TASK-0020
    - TASK-0021
    - TASK-0022
  codeRefs:
    - scripts/ci/parse-trx.sh
    - scripts/testing/verify-critical-coverage.ps1
  pathHistory:
    - "C:/docs/30-contracts/quality-evidence-model.md"
  path: /docs/30-contracts/quality-evidence-model.md
owner: platform
status: draft
updated: 2026-03-07
---

# Quality Evidence Model (Testing V1)

## Summary

This document defines the contract boundary for the proposed Workbench quality
evidence subsystem.

The subsystem has one central rule:

- authored truth is human-owned and lives under `docs/`
- observed truth is generated and lives under `artifacts/`

The quality report is the comparison layer between those two truths. It is not
the source of either one.

## Canonical boundary

| Layer | Canonical location | Owner | Notes |
| --- | --- | --- | --- |
| Authored testing intent | `docs/30-contracts/test-gate.contract.yaml` | human-authored | Compatibility-preserving V1 source for expectations, risk notes, confidence targets, and intentional gaps |
| Observed test inventory | `artifacts/quality/testing/test-inventory.json` | generated | Stable normalized record of discovered tests |
| Observed run results | `artifacts/quality/testing/test-run-summary.json` | generated | Stable normalized record of current run evidence |
| Observed coverage | `artifacts/quality/testing/coverage-summary.json` | generated | Stable normalized record of current coverage evidence |
| Compared report | `artifacts/quality/testing/quality-report.json` and `artifacts/quality/testing/quality-summary.md` | generated | Bridge between authored and observed truth |

## Authored truth

V1 should extend the existing `docs/30-contracts/test-gate.contract.yaml`
instead of replacing it outright.

The proposed authored contract shape is:

```yaml
version: 2
domain: testing

scope:
  includes:
    - src/Workbench.Cli
    - src/Workbench.Core
  excludes:
    - src/Workbench.Tui

expectations:
  evidence:
    - inventory
    - results
    - coverage
  confidenceTarget: high
  requiredTestKinds:
    - unit
    - integration
  criticalFiles:
    - src/Workbench.Core/FrontMatter.cs
    - src/Workbench.Core/SchemaValidationService.cs
  requiredTests:
    - tests/Workbench.IntegrationTests/ResilienceTests.cs::Doctor_NonGitRepo_ReturnsFriendlyErrorWithoutStackTrace

coverage:
  lineMin: 0.55
  branchMin: 0.50

intentionalGaps:
  - subject: src/Workbench.Tui
    rationale: Manual interaction surface still evolving
    relatedWorkItem: TASK-0007

related:
  docs:
    - /docs/10-product/feature-spec-quality-evidence-testing-v1.md
  workItems:
    - TASK-0015
  codeRefs:
    - scripts/testing/verify-critical-coverage.ps1
```

Authored truth is allowed to describe desired confidence and intentional gaps
even when current observed evidence is weak. That is the point of the
separation.

## Observed truth

Observed truth is the normalized artifact set under `artifacts/quality/testing/`
and follows the schemas in `docs/30-contracts/`.

```text
artifacts/
  quality/
    testing/
      test-inventory.json
      test-run-summary.json
      coverage-summary.json
      quality-report.json
      quality-summary.md
```

Rules:

- Raw TRX and coverage XML are inputs only.
- Normalized JSON is the canonical observed record for agents and automation.
- Markdown is the canonical human summary for review inside the repo.
- Stable filenames represent the latest known snapshot; V1 relies on git
  history rather than a separate evidence database.

## Schema set

| File | Purpose |
| --- | --- |
| `docs/30-contracts/test-inventory.schema.json` | Discovered tests and test projects |
| `docs/30-contracts/test-run-summary.schema.json` | Latest normalized run results |
| `docs/30-contracts/coverage-summary.schema.json` | Latest normalized coverage results |
| `docs/30-contracts/quality-report.schema.json` | Compared view of authored truth versus observed truth |

## Command contract

### `workbench quality sync`

Responsibilities:

- read authored testing intent
- discover tests
- ingest run and coverage artifacts
- generate normalized JSON
- generate Markdown summary

Contract rules:

- writes only generated observed-truth artifacts
- does not rewrite authored intent
- uses standard Workbench JSON envelopes
- surfaces partial evidence and warnings instead of silently dropping data

### `workbench quality show`

Responsibilities:

- read normalized artifacts
- default to the latest `quality-report`
- expose the selected artifact as table output or JSON

Contract rules:

- reads normalized artifacts, not raw tool outputs
- does not infer policy or gating state

## Detectable gaps in V1

V1 report generation should detect only evidence gaps that are defensible from
available artifacts:

- required tests missing from inventory
- required tests not present in the latest observed run
- discovered test projects with no current run result
- critical files missing from coverage evidence
- coverage below authored thresholds
- expected evidence kinds missing entirely

V1 should not claim:

- that coverage equals correctness
- that a passing test is meaningful
- that a low-evidence state should block merge

## Relationship to existing repo artifacts

- `docs/30-contracts/test-gate.contract.yaml` becomes the authored starting
  point rather than a standalone gate definition.
- `docs/30-contracts/test-matrix.md` remains a curated mapping doc and can later
  be partially generated from quality evidence, but it is not replaced in V1.
- `scripts/ci/parse-trx.sh` and
  `scripts/testing/verify-critical-coverage.ps1` become implementation seeds for
  the normalized ingestion pipeline rather than parallel one-off utilities.

## Extension path

- Phase 2 adds analyzer and changed-file evidence as new observed evidence
  kinds.
- Phase 3 adds mutation/fuzz evidence via additional normalized schemas.
- Phase 4 adds AI-authored remediation suggestions that remain advisory.
