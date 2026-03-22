# Repository Guidelines

## Project Structure

- `src/Workbench`: tool entry point packaged as the `workbench` .NET tool.
- `src/Workbench.Core`, `src/Workbench.Cli`, `src/Workbench.Tui`: shared implementation and command surface.
- `tests/Workbench.Tests`, `tests/Workbench.IntegrationTests`: unit and integration coverage.
- `overview/`, `contracts/`, `decisions/`, `runbooks/`, `tracking/`, `templates/`, `schemas/`: canonical documentation roots.
- `specs/`: canonical specs.
- `work/`: canonical work items and workboard artifacts.
- `artifacts/`: generated outputs only. Do not hand-edit files there.

## Build, Test, and Quality Commands

- `dotnet build Workbench.slnx -c Release`
- `dotnet test --solution Workbench.slnx`
- `dotnet tool restore`
- `pwsh -File scripts/testing/run-quality-evidence.ps1`
- `dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage`
- `dotnet tool run workbench quality show`

## Quality Evidence Workflow

- Canonical authored intent lives in `contracts/test-gate.contract.yaml`.
- Raw observed evidence lives in `artifacts/quality/raw/test-results/*.trx` and `artifacts/quality/raw/coverage/*.cobertura.xml`.
- Generated quality artifacts live in `artifacts/quality/testing/` and are derived outputs from Workbench. Do not edit them by hand.
- Treat the quality report as advisory evidence for humans and agents. Do not add merge-blocking behavior based on the generated report in this repo.
- Prefer `dotnet tool run workbench` for quality commands so the repo uses the version pinned in `dotnet-tools.json`. Use `workbench.ps1` only when intentionally validating in-repo source changes.

## Agent-Specific Instructions

- If you change test scope, thresholds, or critical areas, update `contracts/test-gate.contract.yaml`.
- If you change the quality workflow, keep the artifact paths above stable unless you also update the docs and agent guidance.
- When summarizing repo quality state, rely on `workbench quality show` or the generated files in `artifacts/quality/testing/`, not on ad hoc TRX or Cobertura parsing.

## Formatting (Required)

- Always run `dotnet format Workbench.slnx` after making code changes.
- Fix any formatting or analyzer issues reported by `dotnet format` before finalizing changes.
