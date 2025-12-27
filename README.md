# Workbench

Workbench is a .NET-based CLI for interacting with the Workbench tooling.

## Quickstart

### Prerequisites

- .NET SDK (latest stable recommended)

### Build

```bash
dotnet build Workbench.slnx
```

## Pack (NuGet tool)

Build the .NET tool package:

```bash
dotnet pack src/Workbench/Workbench.csproj -c Release
```

Run integration tests:

```bash
dotnet test tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

Run GitHub CLI-dependent integration tests:

```bash
WORKBENCH_RUN_GH_TESTS=1 dotnet test tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

## Run (CLI)

Run the automated tests:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- --help
```

### Test

```bash
dotnet test tests/Workbench.Tests/Workbench.Tests.csproj
```

## Verification

Run the full test suite (matches CI expectations):

```bash
dotnet test Workbench.slnx
```

## CI

GitHub Actions runs build and test jobs for each OS/.NET SDK pair in the matrix
(`ubuntu-latest`, `windows-latest`, `macos-latest` with .NET `10.0.x`). The
workflow runs:

```bash
dotnet build Workbench.slnx
dotnet test Workbench.slnx
```

## Build (AOT)

Publish a single native binary:

```bash
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-arm64
```

Replace the runtime identifier with your target (e.g., `win-x64`, `linux-x64`).

Verification steps:

```bash
# Build output
ls src/Workbench/bin/Release/net10.0/osx-arm64/publish

# Run the published binary
./src/Workbench/bin/Release/net10.0/osx-arm64/publish/workbench --help
```

Expected warnings:
- None. Treat any trimming/AOT warnings (IL2026/IL3050) as regressions and address them.

## Command reference

See the full CLI command list and options in `docs/30-contracts/cli-help.md`.

## Contributing

- [Contribution guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security policy](SECURITY.md)
