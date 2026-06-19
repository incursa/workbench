# Runbook: Maintainer Readiness

## Purpose

Use this runbook before changing Workbench command behavior, packaging, release
surfaces, or repository-native documentation conventions.

Workbench is a .NET CLI and local browser tool for repository-native
specifications, architecture docs, work items, verification artifacts,
validation, generated navigation, and quality evidence. Source control remains
the system of record. GitHub issues, pull requests, MCP docs, generated quality
reports, and release artifacts are integration surfaces around that source
model.

## Architecture

The executable entry point is [`src/Workbench/Program.cs`](../src/Workbench/Program.cs).
It routes `workbench web` and `workbench ui` to the local Razor Pages host and
routes every other command to the CLI command tree.

Core boundaries:

- [`src/Workbench`](../src/Workbench): packaged .NET tool entry point and local
  browser UI host.
- [`src/Workbench.Cli`](../src/Workbench.Cli): `System.CommandLine` command
  composition, output normalization, and command dispatch.
- [`src/Workbench.Core`](../src/Workbench.Core): repository IO, config,
  validation, Git/GitHub integration, Spec Trace handling, quality evidence,
  voice transcription, and shared models.
- [`src/Workbench.Tui`](../src/Workbench.Tui): terminal UI entry point and
  interaction helpers.
- [`src/mcp`](../src/mcp): Cloudflare Worker source for the deterministic
  Workbench docs MCP server.
- [`content`](../content): markdown source for the docs MCP server and the
  source tree published through [`docs.site.json`](../docs.site.json).
- [`dist/mcp`](../dist/mcp): generated MCP manifests and bundled Worker output.

The CLI layer should stay thin. Put business rules in `Workbench.Core` and keep
the generated command snapshot in [`specs/generated/commands.md`](../specs/generated/commands.md)
aligned with the live command tree.

## Command And Work Item Workflows

Use the generated command snapshot as the authoritative local reference:
[`specs/generated/commands.md`](../specs/generated/commands.md).

Common repository workflow:

```bash
workbench item new --type work_item --title "Example work item"
workbench spec new --title "Example capability" --domain WB --capability EXAMPLE
workbench spec link --path specs/requirements/WB/SPEC-WB-EXAMPLE.md --work-item WI-WB-0001
workbench nav sync
workbench validate --profile core
```

Common synchronization workflow:

```bash
workbench sync --dry-run
workbench item sync --dry-run
workbench doc sync --all --dry-run
workbench nav sync --dry-run
```

Use write-mode sync only after the dry run is understood:

```bash
workbench sync
```

Use [`workbench.ps1`](../workbench.ps1) only when validating in-repo source
changes. In downstream repositories, prefer the pinned local tool:

```bash
dotnet tool restore
dotnet tool run workbench validate --profile core
```

## Local Build And Test Commands

Run commands from the repository root.

Restore .NET tools:

```bash
dotnet tool restore
```

Build the solution:

```bash
dotnet build Workbench.slnx -c Release
```

Run unit tests:

```bash
dotnet test --project tests/Workbench.Tests/Workbench.Tests.csproj -c Release
```

Run integration tests:

```bash
dotnet test --project tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj -c Release
```

Run all solution tests:

```bash
dotnet test --solution Workbench.slnx -c Release
```

Verify the generated CLI command snapshot:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- doc regen-help --check
```

Validate this repository through the in-repo source build:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- validate --profile core
```

The repository config excludes installed npm packages and vendored UI-kit web
assets from link validation. Keep those exclusions in
[`.workbench/config.json`](../.workbench/config.json) unless the vendored
content becomes authored documentation.

Generate quality evidence:

```powershell
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet run --project src/Workbench/Workbench.csproj -- quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage --out-dir artifacts/quality/testing
dotnet run --project src/Workbench/Workbench.csproj -- quality show
dotnet run --project src/Workbench/Workbench.csproj -- quality proof-health
dotnet run --project src/Workbench/Workbench.csproj -- quality attest
```

Build and test the docs MCP Worker:

```bash
npm install
npm test
```

Pack the .NET tool:

```bash
dotnet pack src/Workbench/Workbench.csproj -c Release
```

Run the whitespace check before committing:

```bash
git diff --check
```

Run `dotnet format Workbench.slnx` after code changes. Documentation-only
changes do not need formatting unless generated code or project files changed.

## Integration Points

Spec Trace:

- Canonical artifacts live under [`specs`](../specs).
- The core project embeds a pinned SpecTrace schema snapshot from
  [`src/Workbench.Core/Workbench.Core.csproj`](../src/Workbench.Core/Workbench.Core.csproj).
- A first restore/build on a clean machine may need network access to download
  that pinned schema before it is embedded as a resource.
- `workbench validate --profile core|traceable|auditable` is the local
  validation surface.

GitHub:

- `.workbench/config.json` controls GitHub provider defaults, owner/repository
  values, branch patterns, and sync behavior.
- `workbench item sync` reconciles work items with issues and branch state.
- `workbench github pr create` creates a PR and can backlink it to a work item.
- GitHub CLI is optional for some integration tests and provider workflows.
- [`docs.site.json`](../docs.site.json) and [`.github/workflows/sync-docs.yml`](../.github/workflows/sync-docs.yml)
  control mirrored docs publication into `incursa-docs`.

Quality and release tooling:

- [`quality/testing-intent.yaml`](../quality/testing-intent.yaml) describes
  authored test intent and critical coverage areas.
- [`quality/attestation.yaml`](../quality/attestation.yaml) describes derived
  attestation defaults.
- [`scripts/testing/run-quality-evidence.ps1`](../scripts/testing/run-quality-evidence.ps1)
  produces standard raw TRX and Cobertura inputs.
- [`scripts/testing/verify-critical-coverage.ps1`](../scripts/testing/verify-critical-coverage.ps1)
  checks critical coverage expectations.
- [`package.json`](../package.json) owns the private npm package for the docs
  MCP Worker, not the .NET CLI package.

AI and voice:

- `workbench doc summarize`, `workbench item generate`, and voice commands use
  configured AI credentials.
- Keep secrets in environment variables or `.workbench/credentials.env`. Do not
  commit credentials.
- Voice transcription expects `OPENAI_API_KEY` or `WORKBENCH_AI_OPENAI_KEY`
  when using the OpenAI transcription path.

## Release And Versioning

The NuGet package ID is `Incursa.Workbench`. The project file currently carries
the local package version, while the publish workflow computes calendar-style
versions for automated package and AOT artifacts.

Maintainer expectations:

- Treat command behavior, output JSON contracts, artifact schemas, and generated
  command docs as public surfaces.
- Regenerate and check [`specs/generated/commands.md`](../specs/generated/commands.md)
  when the command tree changes.
- Use `patch`-level release notes for compatible fixes and docs.
- Use a new minor or calendar release when commands, schemas, quality evidence,
  or package contents change in a way downstream repos must notice.
- Use a breaking release only when command names, output contracts, or canonical
  artifact expectations are intentionally incompatible.
- Do not rely on GitHub Actions as local proof. Reproduce the relevant build,
  test, validation, pack, and MCP checks locally first.
- The docs MCP package is separate from the CLI package and is released from
  the Node workspace in [`package.json`](../package.json).

## Current Readiness State

The repository is ready for local maintainer work when these checks pass:

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

Broader readiness can also include:

```powershell
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet run --project src/Workbench/Workbench.csproj -- quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage --out-dir artifacts/quality/testing
dotnet run --project src/Workbench/Workbench.csproj -- quality show
dotnet run --project src/Workbench/Workbench.csproj -- quality proof-health
dotnet run --project src/Workbench/Workbench.csproj -- quality attest
```

## Known Gaps And Cleanup Needs

- `tracking/workbench-gaps.md` is still the active gap ledger for planned
  linking, docs, hooks, and CLI cleanup.
- Mutation testing has a config file but is not part of the default local gate.
- Fuzz harnesses exist under [`fuzz`](../fuzz), but fuzzing is not part of the
  standard readiness command sequence.
- GitHub-integrated flows require configured provider settings and credentials;
  local tests do not prove remote repository settings.
- The docs MCP Worker has a deploy script, but deployment requires Cloudflare
  configuration and secrets outside the local readiness pass.
- Derived outputs under [`artifacts`](../artifacts), [`specs/generated`](../specs/generated),
  and [`dist/mcp`](../dist/mcp) must not be edited by hand.
