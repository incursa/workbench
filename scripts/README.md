# Scripts

Helper scripts for canonical SpecTrace validation, packaging, and release automation.

## Canonical JSON

- [`scripts/Validate-SpecTraceJson.ps1`](Validate-SpecTraceJson.ps1): validates a target repository's canonical JSON artifacts against the SpecTrace schema snapshot pinned into the local Workbench build. Optionally syncs navigation after validation.

## Testing

- [`scripts/testing/run-quality-evidence.ps1`](testing/run-quality-evidence.ps1): builds the solution, runs the unit and integration test projects, and writes raw quality evidence to `artifacts/quality/raw/test-results/` and `artifacts/quality/raw/coverage/`.
- [`scripts/testing/run-quality-evidence.sh`](testing/run-quality-evidence.sh): bash equivalent of the quality-evidence runner for CI or Unix-like environments.
- [`scripts/testing/verify-critical-coverage.ps1`](testing/verify-critical-coverage.ps1): validates critical-surface coverage thresholds and required scenario tests declared in [`quality/testing-intent.yaml`](../quality/testing-intent.yaml).

## Quality evidence paths

- Authored intent: [`quality/testing-intent.yaml`](../quality/testing-intent.yaml)
- Raw observed test results: `artifacts/quality/raw/test-results/*.trx`
- Raw observed coverage: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated Workbench summaries: `artifacts/quality/testing/`
