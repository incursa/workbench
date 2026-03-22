# Scripts

Helper scripts for packaging and release automation.

## Testing

- `scripts/testing/run-quality-evidence.ps1`: builds the solution, runs the unit and integration test projects, and writes raw quality evidence to `artifacts/quality/raw/test-results/` and `artifacts/quality/raw/coverage/`.
- `scripts/testing/run-quality-evidence.sh`: bash equivalent of the quality-evidence runner for CI or Unix-like environments.
- `scripts/testing/verify-critical-coverage.ps1`: validates critical-surface coverage thresholds and required scenario tests declared in `quality/testing-intent.yaml`.

## Quality evidence paths

- Authored intent: `quality/testing-intent.yaml`
- Raw observed test results: `artifacts/quality/raw/test-results/*.trx`
- Raw observed coverage: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated Workbench summaries: `artifacts/quality/testing/`
