# Workbench.Benchmarks

This directory contains permanent BenchmarkDotNet suites for the validation and parsing hot paths in `Workbench`.

## Included Suites

- `CanonicalValidationBenchmarks`

## Run

```bash
dotnet run -c Release --project benchmarks/Workbench.Benchmarks.csproj -- --job Dry --filter "*CanonicalValidationBenchmarks*"
```

Use `--filter` to narrow to a subset of benchmarks when iterating locally.
