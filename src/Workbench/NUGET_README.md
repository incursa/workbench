# Incursa Workbench

Incursa Workbench is a .NET CLI tool for teams that manage engineering work
in-repo and want consistent, structured documentation and work item workflows.

Canonical SpecTrace artifacts are JSON documents validated against the target
repository's `model/model.schema.json`.

It provides commands to:

- scaffold and maintain the canonical Spec Trace artifact families and repository guidance
- create, update, and sync work items
- link repository specs, architecture docs, verification artifacts, and work items
- run repository validation and consistency checks
- open a local browser UI for browsing and editing work items

## Installation

Install as a global tool:

```bash
dotnet tool install --global Incursa.Workbench
```

Install as a local tool (recommended for team repos):

```bash
dotnet new tool-manifest
dotnet tool install Incursa.Workbench
```

## Quick Start

Initialize Workbench in a repository:

```bash
workbench init
```

List commands:

```bash
workbench --help
```

Run diagnostics:

```bash
workbench doctor
```

Open the local browser UI:

```bash
workbench web
```

## Typical Workflows

Create a work item:

```bash
workbench item new --type work_item --title "Example work item"
```

Create a document:

```bash
workbench doc new --type architecture --title "Example design note"
```

Sync specs, docs, and links:

```bash
workbench doc sync --all
```

## Requirements

- .NET SDK 10.0.100 or later
- Optional: GitHub CLI for GitHub-integrated workflows

## Documentation

Project documentation and contracts are available in the repository:

- [`README.md`](../../README.md)
- [`overview.md`](../../overview.md)
- [`layout.md`](../../layout.md)
- [`authoring.md`](../../authoring.md)
- [`specs/generated/commands.md`](../../specs/generated/commands.md)
