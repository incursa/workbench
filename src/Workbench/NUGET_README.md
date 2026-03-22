# Incursa Workbench

Incursa Workbench is a .NET CLI tool for teams that manage engineering work in-repo and want consistent, structured documentation and work item workflows.

It provides commands to:

- scaffold and maintain Workbench overview/specs/contracts/decisions/runbooks/tracking/work item structure
- create, update, and sync work items
- link repository docs, architecture decisions, and work items
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
workbench item new --type task --title "Example task"
```

Create a document:

```bash
workbench doc new --type doc --title "Example design note"
```

Sync docs and links:

```bash
workbench doc sync --all
```

## Requirements

- .NET SDK 10.0.100 or later
- Optional: GitHub CLI for GitHub-integrated workflows

## Documentation

Project documentation and contracts are available in the repository:

- `overview/README.md`
- `contracts/cli-help.md`
