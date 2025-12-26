# Workbench

## Build (local)

Build the solution:

```bash
dotnet build Workbench.slnx
```

## Pack (NuGet tool)

Build the .NET tool package:

```bash
dotnet pack src/Workbench/Workbench.csproj -c Release
```

## Test

Run the automated tests:

```bash
dotnet test tests/Workbench.Tests/Workbench.Tests.csproj
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
