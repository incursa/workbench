# Workbench Test Matrix

Generated overview of the current test and evidence surfaces.
Keep this file aligned with the active suite and the authored quality intent.

## Core Coverage

| Surface | Command | Notes |
| --- | --- | --- |
| Unit tests | `dotnet test --project tests/Workbench.Tests/Workbench.Tests.csproj` | Fast validation for parsing, normalization, and core helpers. |
| Integration tests | `dotnet test --project tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj` | Exercises CLI flows, repository shaping, sync, and validation. |
| Full solution | `dotnet test --solution Workbench.slnx` | Standard release gate for the repo. |

## Quality Evidence

| Surface | Command | Notes |
| --- | --- | --- |
| Raw evidence | `pwsh -File scripts/testing/run-quality-evidence.ps1` | Produces TRX and Cobertura artifacts in `artifacts/quality/raw/`. |
| Sync report | `dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage` | Normalizes raw evidence into the repo-native quality report. |
| Report view | `dotnet tool run workbench quality show` | Shows the current quality summary and findings. |

## GitHub-Dependent Coverage

- Set `WORKBENCH_RUN_GH_TESTS=1` to include GitHub-dependent integration coverage.
- Leave it unset for the offline, repo-local test path.
