---
name: workbench-quality
description: Generate and inspect repo-native testing evidence for Workbench quality reports.
---

## Core workflow

1. Treat `docs/30-contracts/test-gate.contract.yaml` as the canonical authored testing-intent contract.
2. Restore the pinned tool with `dotnet tool restore`.
3. Generate raw evidence with `pwsh -File scripts/testing/run-quality-evidence.ps1` or `bash ./scripts/testing/run-quality-evidence.sh`.
4. Normalize and summarize with `dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage`.
5. Inspect the current report with `dotnet tool run workbench quality show` or `dotnet tool run workbench quality show --format json`.

## Path conventions

- Authored intent: `docs/30-contracts/test-gate.contract.yaml`
- Raw test results: `artifacts/quality/raw/test-results/*.trx`
- Raw coverage: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated quality artifacts: `artifacts/quality/testing/`

## Guardrails

- Generated artifacts under `artifacts/quality/testing/` are derived. Do not hand-edit them.
- Keep the quality report advisory only. Do not add merge blocking or policy enforcement in this workflow.
- When test scope changes, update the authored contract before trusting the generated summary.
