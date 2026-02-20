---
workbench:
  type: guide
  workItems:
    - TASK-0011
  codeRefs: []
owner: platform
status: active
updated: 2026-01-01
---

# Getting Started with Workbench

Welcome to Workbench! This guide will walk you through installing and using
Workbench to manage documentation, work items, and contracts in your repository.

## What is Workbench?

Workbench is a .NET CLI tool that helps you manage structured documentation and
work items directly in your git repository. It provides:

- Work item tracking with GitHub integration
- Structured documentation with front matter and schemas
- Navigation and validation tools
- Voice-powered documentation and work item creation
- Terminal UI for interactive workflows

## Prerequisites

- .NET SDK `10.0.100` or later (check with `dotnet --version`)
- Git installed and configured
- A git repository (existing or new)
- Optional: GitHub CLI (`gh`) for GitHub integration
- Optional: OpenAI API key for AI-powered features

## Installation

### Option 1: Build from source (current)

Clone the Workbench repository and build it:

```bash
git clone https://github.com/bravellian/workbench.git
cd workbench
dotnet build Workbench.slnx
```

Run Workbench using:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- <command>
```

Or create an alias for convenience:

```bash
# Add to your ~/.bashrc or ~/.zshrc
alias workbench='dotnet run --project /path/to/workbench/src/Workbench/Workbench.csproj --'
```

### Option 2: Install as a .NET tool (future)

Once published to NuGet, you'll be able to install globally:

```bash
dotnet tool install -g Workbench
```

### Option 3: Use a native binary

Build a native binary for your platform:

```bash
# macOS ARM64
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-arm64

# macOS x64
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-x64

# Linux x64
dotnet publish src/Workbench/Workbench.csproj -c Release -r linux-x64

# Windows x64
dotnet publish src/Workbench/Workbench.csproj -c Release -r win-x64
```

The binary will be in `src/Workbench/bin/Release/net10.0/<runtime>/publish/`.

## Quick Start

### 1. Initialize a repository

Navigate to your git repository (or create a new one):

```bash
mkdir my-project
cd my-project
git init
```

Run the initialization wizard:

```bash
workbench init
```

This interactive wizard will:
- Create the default folder structure (`docs/`, `docs/70-work/`, etc.)
- Set up configuration in `.workbench/config.json`
- Create templates for work items and documentation
- Optionally configure OpenAI integration
- Guide you through credential storage options

For non-interactive setup:

```bash
workbench init --non-interactive --skip-wizard
```

### 2. Verify your setup

Check that everything is configured correctly:

```bash
workbench doctor
```

This command validates:
- Git is installed and the repository is initialized
- Configuration is valid
- Required directories exist
- GitHub provider is configured (if applicable)

### 3. Create your first work item

#### Using the interactive wizard

Launch the wizard:

```bash
workbench run
```

Follow the prompts to create a work item with the right type, title, and metadata.

#### Using the command line

Create a task directly:

```bash
workbench item new --type task --title "Set up project documentation" --priority high
```

This creates a new work item file in `docs/70-work/items/` with:
- A unique ID (e.g., `TASK-0001`)
- YAML front matter with metadata
- A markdown body with sections for summary and acceptance criteria

#### Using voice input

Record a work item using your voice:

```bash
workbench voice workitem --type task
```

Requires `OPENAI_API_KEY` environment variable for transcription.

### 4. View and manage work items

List all open work items:

```bash
workbench item list
```

Show details of a specific item:

```bash
workbench item show TASK-0001
```

Update the status of a work item:

```bash
workbench item status TASK-0001 in-progress
```

Close a completed work item:

```bash
workbench item close TASK-0001 --move
```

The `--move` flag moves the item to `docs/70-work/done/`.

### 5. Create documentation

Create a feature specification:

```bash
workbench doc new --type spec --title "User authentication flow" --work-item TASK-0001
```

Create an architecture decision record:

```bash
workbench doc new --type adr --title "Use JWT for authentication"
```

Create a runbook:

```bash
workbench doc new --type runbook --title "Deploy to production"
```

### 6. Sync and validate

Sync work items with GitHub issues:

```bash
workbench sync --items --issues
```

Update navigation and indexes:

```bash
workbench nav sync
```

Validate all work items, links, and schemas:

```bash
workbench validate
```

Use `--strict` in CI to treat warnings as errors:

```bash
workbench validate --strict
```

## Common Workflows

### Creating a work item with a branch and PR

Use the `promote` command for the full workflow:

```bash
workbench promote --type task --title "Add user search feature" --start --pr --draft
```

This:
1. Creates a work item
2. Creates and checks out a branch
3. Makes an initial commit
4. Creates a draft GitHub PR
5. Links the PR to the work item

### Importing GitHub issues as work items

Import one or more issues:

```bash
workbench item import --issue 42 --issue 18
```

Or sync all unlinked issues:

```bash
workbench sync --import-issues
```

### Working with documentation

Create a spec and link it to a work item:

```bash
workbench doc new --type spec --title "Payment processing" --work-item TASK-0005
```

Link an existing doc to a work item:

```bash
workbench item link TASK-0005 --spec docs/10-product/payment-spec.md
```

### Using AI features

Generate a work item from freeform text:

```bash
workbench item generate --prompt "Add support for exporting data to CSV format"
```

Summarize documentation changes:

```bash
workbench doc summarize --staged
```

## Configuration

Workbench reads configuration from `.workbench/config.json`. View the current config:

```bash
workbench config show
```

Update a specific setting:

```bash
workbench config set --path github.owner --value "myorg"
```

### GitHub Integration

Workbench supports two GitHub providers:

1. **gh CLI** (recommended): Uses the GitHub CLI's authentication
2. **Octokit**: Uses a personal access token

Set your provider in config:

```bash
workbench config set --path github.provider --value "gh"
```

For Octokit, store your token securely:

```bash
workbench config credentials set --key GITHUB_TOKEN --value "ghp_..."
```

### OpenAI Integration

For AI-powered features (voice transcription, work item generation), configure OpenAI:

```bash
workbench config credentials set --key WORKBENCH_AI_OPENAI_KEY --value "sk-..."
```

Optional settings:

```bash
export WORKBENCH_AI_TRANSCRIPTION_MODEL="gpt-4o-mini-transcribe"
export WORKBENCH_AI_TRANSCRIPTION_LANGUAGE="en"
```

## Next Steps

- **Explore the [CLI reference](../30-contracts/cli-help.md)** for all available commands
- **Read the [feature spec](../10-product/feature-spec-cli-onboarding-wizard.md)** for the onboarding wizard
- **Try the [sample walkthrough](../../examples/sample-walkthrough/)** for a hands-on example
- **Check the [documentation structure](documentation-structure.md)** to understand how docs are organized
- **Review [ADRs](../40-decisions/README.md)** to understand architectural decisions

## Troubleshooting

### "Not a git repository" error

Ensure you're in a git repository:

```bash
git init
```

### "GitHub provider not configured" warning

Either install and authenticate with `gh`:

```bash
gh auth login
```

Or configure an Octokit token as described in the GitHub Integration section.

### ".NET SDK not found" error

Install .NET SDK 10.0.100 or later from [dotnet.microsoft.com](https://dotnet.microsoft.com/).

### Voice features not working

Ensure you have:
- Set `OPENAI_API_KEY` environment variable
- Granted microphone permissions to your terminal (macOS: System Settings → Privacy & Security → Microphone)

## Getting Help

- **CLI help**: Run `workbench --help` or `workbench <command> --help`
- **Documentation**: Browse the `docs/` directory
- **Issues**: Report issues at [github.com/bravellian/workbench/issues](https://github.com/bravellian/workbench/issues)
- **Community**: Join discussions at [github.com/bravellian/workbench/discussions](https://github.com/bravellian/workbench/discussions)
