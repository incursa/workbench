# Scripts

Helper scripts for canonical SpecTrace validation, packaging, and release automation.

## Canonical JSON

- [`scripts/Test-SpecTraceRepository.ps1`](Test-SpecTraceRepository.ps1): supported front door for validating a target repository's canonical JSON artifacts against the SpecTrace schema snapshot pinned into the local Workbench build. Same-stem rendered Markdown companions are allowed and ignored when JSON exists beside them. Repo-native requirement `coverage` blocks and `status: "landed"` artifacts are normalized during validation. Optionally syncs navigation after validation.
- [`scripts/Validate-SpecTraceJson.ps1`](Validate-SpecTraceJson.ps1): compatibility wrapper for older callers. Delegates to [`scripts/Test-SpecTraceRepository.ps1`](Test-SpecTraceRepository.ps1).

## Testing

- [`scripts/testing/run-quality-evidence.ps1`](testing/run-quality-evidence.ps1): builds the solution, runs the unit and integration test projects, and writes raw quality evidence to `artifacts/quality/raw/test-results/` and `artifacts/quality/raw/coverage/`.
- [`scripts/testing/run-quality-evidence.sh`](testing/run-quality-evidence.sh): bash equivalent of the quality-evidence runner for CI or Unix-like environments.
- [`scripts/testing/verify-critical-coverage.ps1`](testing/verify-critical-coverage.ps1): validates critical-surface coverage thresholds and required scenario tests declared in [`quality/testing-intent.yaml`](../quality/testing-intent.yaml).

## Quality evidence paths

- Authored intent: [`quality/testing-intent.yaml`](../quality/testing-intent.yaml)
- Raw observed test results: `artifacts/quality/raw/test-results/*.trx`
- Raw observed coverage: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated Workbench summaries: `artifacts/quality/testing/`
