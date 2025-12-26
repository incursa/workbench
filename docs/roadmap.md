# Workman Roadmap

## Version 0.1.0 (MVP) ✅

**Goal**: Minimal viable product with core functionality

- [x] Project structure and solution setup
- [x] Core documentation (spec, commands, format)
- [x] Basic CLI infrastructure (System.CommandLine)
- [x] Version and doctor commands
- [ ] Git integration (repository detection)
- [ ] Work item models and front matter parsing
- [ ] File I/O abstractions

**Status**: In Progress

## Version 0.2.0 (Work Item Management)

**Goal**: Create and manage work items

- [ ] `workman init` - Initialize repository
- [ ] `workman new` - Create work items
- [ ] `workman list` - List and filter work items
- [ ] `workman show` - Display work item details
- [ ] `workman update` - Update work item metadata
- [ ] `workman validate` - Validate work items
- [ ] ID generation and uniqueness
- [ ] Template system
- [ ] Configuration file support (.workman.yml)

**Target**: Q1 2025

## Version 0.3.0 (Search and Organization)

**Goal**: Find and organize work items

- [ ] `workman search` - Full-text search
- [ ] `workman stats` - Statistics and reporting
- [ ] Tag management
- [ ] Work item relationships (related, blocks, blocked_by)
- [ ] Archive/move to done
- [ ] Custom work item types

**Target**: Q2 2025

## Version 0.4.0 (Git Integration)

**Goal**: Deep Git integration

- [ ] `workman branch` - Create branches from work items
- [ ] Commit message integration
- [ ] Branch status detection
- [ ] Work item status from Git state
- [ ] Interactive rebase helpers
- [ ] Git hooks for validation

**Target**: Q2 2025

## Version 0.5.0 (GitHub Integration)

**Goal**: GitHub CLI integration

- [ ] Link to GitHub Issues
- [ ] Link to GitHub Pull Requests
- [ ] Sync status with GitHub
- [ ] Create PRs from work items
- [ ] Import from GitHub Issues
- [ ] Export to GitHub

**Target**: Q3 2025

## Version 0.6.0 (Advanced Features)

**Goal**: Power user features

- [ ] Custom templates
- [ ] Workflow automation
- [ ] Webhooks/triggers
- [ ] Plugin system
- [ ] Interactive TUI mode
- [ ] Bulk operations

**Target**: Q3 2025

## Version 1.0.0 (Stable Release)

**Goal**: Production-ready, stable, well-documented

- [ ] Complete documentation
- [ ] Performance optimization
- [ ] Comprehensive error handling
- [ ] Migration guides
- [ ] Best practices guide
- [ ] Video tutorials
- [ ] Community templates

**Target**: Q4 2025

## Future Considerations

### Integrations

- **Jira**: Import/export work items
- **Azure DevOps**: Sync with boards
- **Linear**: Bidirectional sync
- **Slack**: Notifications and commands
- **VS Code**: Extension for work item management
- **GitHub Actions**: Automated workflows

### Advanced Features

- **Time Tracking**: Log time spent on work items
- **Burndown Charts**: Visual progress tracking
- **Dependencies**: Advanced dependency management
- **Milestones**: Group work items by milestone
- **Labels**: Rich labeling system
- **Comments**: Threaded discussions on work items
- **Attachments**: Link files and screenshots
- **Notifications**: Email/webhook notifications

### Developer Experience

- **Shell Completion**: Bash/Zsh/Fish completion scripts
- **Config Wizard**: Interactive configuration
- **Migration Tools**: Import from other systems
- **Export Formats**: HTML, PDF, CSV exports
- **API Server**: Optional REST API for integrations
- **Web UI**: Simple web interface for browsing

### Enterprise Features

- **Multi-repo**: Aggregate work items across repos
- **LDAP/SSO**: Enterprise authentication
- **Audit Logs**: Track all changes
- **Compliance**: SOC2, GDPR support
- **Analytics**: Advanced metrics and dashboards
- **Custom Fields**: Extensible metadata schema

## Community Requests

This section will track feature requests from the community. Submit ideas via GitHub Issues.

## Non-Goals

These are explicitly **not** planned:

- ❌ Hosted service (Workman is CLI-only, self-hosted)
- ❌ Real-time collaboration (files are version-controlled)
- ❌ Built-in CI/CD (use GitHub Actions, etc.)
- ❌ Code generation (outside of scaffolding)
- ❌ Database backend (files only)
- ❌ Complex project management (Gantt charts, resource allocation)
- ❌ Team chat/communication

## Contributing

We welcome contributions! See CONTRIBUTING.md for guidelines.

Priority areas:
1. Core features (0.1.0 - 0.3.0)
2. Documentation and examples
3. Bug fixes and stability
4. Performance improvements
5. Tests and test coverage

## Versioning

Workman follows [Semantic Versioning](https://semver.org/):
- **MAJOR**: Breaking changes to CLI or file format
- **MINOR**: New features, backward-compatible
- **PATCH**: Bug fixes, no new features

## Release Cadence

- **Minor releases**: Every 6-8 weeks
- **Patch releases**: As needed for critical bugs
- **Major releases**: Only when necessary (breaking changes)

## Deprecation Policy

- Features marked deprecated will be removed after 2 minor versions
- Deprecated features will show warnings
- Migration guides will be provided
- Breaking changes will be clearly documented

## Feedback

Share feedback and suggestions:
- GitHub Issues: Feature requests and bug reports
- Discussions: Questions and ideas
- Twitter: @workman_cli (planned)
