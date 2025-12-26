# Workbench

## Build (local)

Build the solution:

```bash
dotnet build Workbench.slnx
```

## Test

Run the automated tests:

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

## Run (CLI)

Run the CLI locally:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- --help
```

## Verification

Run the full test suite (matches CI expectations):

```bash
dotnet test Workbench.slnx
```

## Build (AOT)

Publish a single native binary:

```bash
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-arm64
```

Replace the runtime identifier with your target (e.g., `win-x64`, `linux-x64`).

## Command reference

See the full CLI command list and options in `docs/30-contracts/cli-help.md`.
