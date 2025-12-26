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
dotnet test tests/Workbench.Tests/Workbench.Tests.csproj
```

Run the full solution verification:

```bash
dotnet test Workbench.slnx
```

## Submitting changes

1. Create a feature branch.
2. Make your changes with clear, focused commits.
3. Ensure tests pass locally.
4. Open a pull request with:
   - A short summary of the change.
   - Links to any related issues or context.
   - Screenshots or logs when behavior changes.

By participating, you agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).
