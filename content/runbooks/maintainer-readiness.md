---
title: "Maintainer readiness"
---

---
uri: workbench://runbooks/maintainer-readiness
slug: maintainer-readiness
title: Maintainer readiness
summary: Local build, validation, packaging, release, and integration checklist for Workbench maintainers.
kind: guide
group: runbooks
aliases:
  - readiness
  - maintainer-runbook
  - local-validation
relatedUris:
  - workbench://overview
  - workbench://runbooks/spec-cli-workflow
  - workbench://specs/public-surface
  - workbench://specs/verification-index
priority: 88
includeInSearch: true
searchKind: guide
tags:
  - runbook
  - maintainer
  - validation
  - release
---

# Maintainer readiness

Workbench is a .NET CLI and local browser tool for repository-native
specifications, architecture docs, work items, verification artifacts,
validation, generated navigation, and quality evidence. The source repository
is authoritative; GitHub issues, pull requests, MCP docs, generated quality
reports, and release artifacts are integration surfaces around that model.

## Architecture

- `src/Workbench`: packaged .NET tool entry point and local browser UI host.
- `src/Workbench.Cli`: command tree, output shape, and dispatch.
- `src/Workbench.Core`: repository IO, config, validation, Git/GitHub
  integration, Spec Trace handling, quality evidence, voice transcription, and
  shared models.
- `src/Workbench.Tui`: terminal UI entry point and helpers.
- `src/mcp` plus `content`: deterministic docs MCP Worker source.
- `dist/mcp`: generated MCP manifests and bundled Worker output.

The CLI layer should stay thin. Business rules belong in `Workbench.Core`.

## Local gate

Run from the repository root:

```bash
dotnet tool restore
dotnet build Workbench.slnx -c Release
dotnet test --project tests/Workbench.Tests/Workbench.Tests.csproj -c Release
dotnet test --project tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj -c Release
dotnet run --project src/Workbench/Workbench.csproj -- doc regen-help --check
dotnet run --project src/Workbench/Workbench.csproj -- validate --profile core
npm test
dotnet pack src/Workbench/Workbench.csproj -c Release
git diff --check
```

The repository config excludes installed npm packages and vendored UI-kit web
assets from link validation. Keep those exclusions unless those folders become
authored documentation.

For quality evidence:

```powershell
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet run --project src/Workbench/Workbench.csproj -- quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage --out-dir artifacts/quality/testing
dotnet run --project src/Workbench/Workbench.csproj -- quality show
dotnet run --project src/Workbench/Workbench.csproj -- quality proof-health
dotnet run --project src/Workbench/Workbench.csproj -- quality attest
```

## Integrations

- Spec Trace validation uses the schema snapshot pinned into the Workbench Core
  project.
- GitHub sync and PR creation depend on `.workbench/config.json` plus provider
  credentials.
- The private npm package builds the docs MCP Worker, not the .NET CLI package.
- AI and voice commands require configured credentials. Do not commit secrets.

## Release expectations

Treat command behavior, output JSON contracts, artifact schemas, generated help,
and package contents as public surfaces. Regenerate `specs/generated/commands.md`
when command behavior changes. Reproduce the relevant local build, test,
validation, package, and MCP checks before relying on CI or publishing.

## Known gaps

- `tracking/workbench-gaps.md` remains the active gap ledger.
- Mutation and fuzzing surfaces exist but are not part of the default local gate.
- GitHub and Cloudflare settings require external verification.
- Generated outputs under `artifacts`, `specs/generated`, and `dist/mcp` are
  derived and should not be edited by hand.
