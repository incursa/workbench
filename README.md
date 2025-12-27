# Workbench

Workbench is a .NET CLI for managing Workbench documentation, work items, and
contracts in this repo.

## Repository map

- `src/Workbench`: CLI source code.
- `tests/`: automated tests.
- `docs/`: product, architecture, contracts, decisions, and runbooks.
- `work/`: active and completed work items plus templates.
- `assets/`: static assets used by docs or tooling.
- `artifacts/`: build outputs and local artifacts.
- `testdata/`: fixtures for parsing and validation tests.

## Requirements

- .NET SDK `10.0.100` (see `global.json`).
- Optional: GitHub CLI for the GH-dependent integration tests.

## Common commands

Build the solution:

```bash
dotnet build Workbench.slnx
```

Run the CLI:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- --help
```

Run tests:

```bash
dotnet test Workbench.slnx
```

Run unit tests only:

```bash
dotnet test tests/Workbench.Tests/Workbench.Tests.csproj
```

Run integration tests:

```bash
dotnet test tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

Run GitHub CLI-dependent integration tests:

```bash
WORKBENCH_RUN_GH_TESTS=1 dotnet test tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

Pack the .NET tool:

```bash
dotnet pack src/Workbench/Workbench.csproj -c Release
```

Publish a native binary (AOT):

```bash
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-arm64
```

Replace the runtime identifier with your target (e.g., `win-x64`, `linux-x64`).

Verification steps:

```bash
ls src/Workbench/bin/Release/net10.0/osx-arm64/publish
./src/Workbench/bin/Release/net10.0/osx-arm64/publish/workbench --help
```

Expected warnings:
- None. Treat any trimming/AOT warnings (IL2026/IL3050) as regressions.

## Documentation and contracts

- Docs overview: `docs/README.md`
- Work items: `work/README.md`
- CLI help: `docs/30-contracts/cli-help.md`
- Schemas: `docs/30-contracts/`

## CI

GitHub Actions builds and tests on `ubuntu-latest`, `windows-latest`, and
`macos-latest` with .NET `10.0.x`:

```bash
dotnet build Workbench.slnx
dotnet test Workbench.slnx
```

## Contributing

- [Contribution guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security policy](SECURITY.md)
