# Contributing to Workbench

Thanks for your interest in improving Workbench! This guide covers local setup, running tests, and submitting changes.

## Setup

### Prerequisites

- .NET SDK (latest stable recommended)

### Get the code

```bash
git clone <repo-url>
cd workbench
```

### Build locally

```bash
dotnet build Workbench.slnx
```

## Tests

Run the targeted test project:

```bash
dotnet test --project tests/Workbench.Tests/Workbench.Tests.csproj
```

Run the full solution verification:

```bash
dotnet test --solution Workbench.slnx
```

Generate the standard raw quality evidence set:

```powershell
dotnet tool restore
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage
dotnet tool run workbench quality show
```

The authored quality intent is [`quality/testing-intent.yaml`](quality/testing-intent.yaml).
Raw test and coverage inputs belong under `artifacts/quality/raw/`.
Generated quality artifacts under `artifacts/quality/testing/` are derived and should not be edited by hand.

Verify the checked-in CLI help snapshot matches the live command tree:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- doc regen-help --check
```

For integration tests that need git, use `GitTestRepo.RunGit` rather than raw
`ProcessRunner.Run(..., "git", ...)` so test repos stay hermetic and ignore
host-machine hooks, signing, and global git config.

## Submitting changes

1. Create a feature branch.
2. Make your changes with clear, focused commits.
3. Ensure tests pass locally.
4. If you changed code or test scope, refresh the quality evidence flow above.
5. Open a pull request with:
   - A short summary of the change.
   - Links to any related issues or context.
   - Screenshots or logs when behavior changes.

By participating, you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).
