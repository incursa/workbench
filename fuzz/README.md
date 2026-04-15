# Workbench.Fuzz

This project contains SharpFuzz harnesses for parser and canonical JSON intake code in `Workbench`.

## Purpose

- Feed arbitrary byte sequences into parser and validation entry points.
- Fail fast on unexpected exceptions.
- Exercise front matter parsing, requirement-clause parsing, and canonical JSON normalization/validation.

## Build

```bash
dotnet build fuzz/Workbench.Fuzz.csproj -c Release
```

Drive the harness with your preferred SharpFuzz runner or command-line tooling.
