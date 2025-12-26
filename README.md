# Workman

A lightweight, file-based work item management CLI tool for Git repositories.

## Overview

**Workman** helps you manage tasks, bugs, spikes, and documentation as Markdown files with YAML front matter, all version-controlled alongside your code.

## Features

- 📝 **File-based**: Work items are Markdown files with YAML front matter
- 🔄 **Git-integrated**: Version-controlled, branch-aware, PR-ready
- 🎨 **Beautiful CLI**: Powered by Spectre.Console with tables and colors
- 🔍 **Searchable**: Full-text search across work items
- 🏷️ **Organized**: Tags, priorities, statuses, and relationships
- 🚀 **Extensible**: Custom templates and configuration

## Quick Start

### Installation

```bash
# Clone and build
git clone https://github.com/bravellian/workbench.git
cd workbench
dotnet build
dotnet run --project src/Workman.Cli/Workman.Cli.csproj
```

### Initialize in Your Project

```bash
cd your-project
workman init
```

### Create a Work Item

```bash
workman new task --title "Add user authentication" --priority high
workman new bug --title "Fix null pointer exception"
workman new spike --title "Evaluate database options"
```

### List Work Items

```bash
workman list
workman list --status in-progress
workman list bug --priority high
```

## Documentation

- [Product Specification](docs/spec.md) - What Workman is and how it works
- [Commands Reference](docs/commands.md) - All available commands
- [Work Item Format](docs/work-item-format.md) - Front matter schema
- [Roadmap](docs/roadmap.md) - Planned features

## Requirements

- .NET 8.0 SDK or later
- Git 2.x or later
- (Optional) GitHub CLI (`gh`) for GitHub integration

## Project Structure

```
workbench/
├── src/
│   ├── Workman.Cli/        # Console application and command wiring
│   ├── Workman.Core/       # Models, front matter, validation
│   ├── Workman.IO/         # Filesystem abstractions
│   ├── Workman.Git/        # Git integration
│   └── Workman.GitHub/     # GitHub CLI integration
├── tests/
│   └── Workman.Tests/      # Unit tests
├── docs/                   # Documentation
├── templates/              # Work item templates
└── Workman.sln             # Solution file
```

## Development

### Build

```bash
dotnet build
```

### Run Tests

```bash
dotnet test
```

### Run CLI

```bash
dotnet run --project src/Workman.Cli/Workman.Cli.csproj -- --help
```

## Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [System.CommandLine](https://github.com/dotnet/command-line-api)
- Styled with [Spectre.Console](https://spectreconsole.net/)
- YAML parsing via [YamlDotNet](https://github.com/aaubry/YamlDotNet)