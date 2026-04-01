# Workbench

Workbench is a .NET CLI for repo-native specifications, architecture, work
items, verification, validation, and generated navigation that all live in
source control. The canonical model for authored intent is Spec Trace:

- specifications group related requirements
- requirements are the atomic normative statements
- architecture explains how requirements are satisfied
- work items describe implementation work
- verification artifacts record how requirements were proven

GitHub remains an optional sync and mirror layer, not the primary system of
record.

Canonical spec-trace artifacts are JSON documents validated against the
SpecTrace model snapshot pinned into the Workbench build. Authored artifacts
may still reference the published remote schema URL for editor assistance and
external tooling, but Workbench does not depend on a repository-local schema
copy. Legacy Markdown with front matter is still supported for repo docs and
older local content, but when a canonical JSON artifact exists beside a
Markdown sibling, Workbench treats the JSON file as the authoritative source.

## Operating model

- Keep canonical requirements in `specs/requirements/`.
- Keep architecture docs in `specs/architecture/`.
- Keep work items in `specs/work-items/`.
- Keep verification artifacts in `specs/verification/`.
- Keep generated repository views under `specs/generated/`.
- Keep canonical templates under `specs/templates/`.
- Keep the quality intent contract in [`quality/testing-intent.yaml`](quality/testing-intent.yaml).
- Keep the attestation config in [`quality/attestation.yaml`](quality/attestation.yaml) when you want repo-local evidence rollup defaults.
- Treat `overview/`, `contracts/`, `decisions/`, `work/`, and the old root
  template/schema copies as removed legacy surfaces.

## Happy path

1. Use `workbench spec new` for requirement specifications.
2. Use `workbench item new` for work items.
3. Use `workbench doc new` for architecture and verification artifacts if you
   need a generic path, or edit the Markdown directly when the artifact already
   exists.
4. Use `workbench item link` to connect work items to specs, architecture docs,
   and verification artifacts.
5. Refresh generated views with `workbench nav sync` and run
   `workbench validate` before review or automation. Use
   `--profile traceable` or `--profile auditable` when you need stronger graph
   checks, and `--scope <path>` to focus validation on a subtree.
6. Agents should prefer `workbench llm help` and `--format json`.

## Sync model

- Use `workbench sync` for the common repo-wide happy path. It runs the lower
  level sync stages.
- Use `workbench item sync` when you need to reconcile local work items with
  GitHub issues or branch state.
- Use `workbench doc sync` when you need to repair or refresh doc front matter
  and backlinks.
- Use `workbench nav sync` when you need to rebuild derived repo indexes.

## Repository map

- `src/Workbench`: CLI source code.
- `tests/`: automated tests.
- `specs/requirements/`: canonical requirement specs and generated Spec Trace outputs.
- `specs/architecture/`: canonical architecture docs.
- `specs/verification/`: canonical verification artifacts.
- `specs/work-items/`: canonical work items and indexes.
- `specs/templates/`: canonical copy-ready templates.
- `specs/schemas/`: JSON schemas for canonical front matter and trace blocks.
- `quality/`: local quality-intent inputs.
- `assets/`: static assets used by docs or tooling.
- `artifacts/`: build outputs and local artifacts.
- `testdata/`: fixtures for parsing and validation tests.

## Requirements

- .NET SDK `10.0.100` (see [`global.json`](global.json)).
- Canonical JSON validation uses the SpecTrace schema snapshot pinned in
  [`src/Workbench.Core/Workbench.Core.csproj`](src/Workbench.Core/Workbench.Core.csproj).
- Optional: GitHub CLI for the GH-dependent integration tests.

## Common commands

Build the solution:

```bash
dotnet build Workbench.slnx
```

Run the CLI:

```bash
dotnet run --project src/Workbench/Workbench.csproj -- --help
```

Run tests:

```bash
dotnet test --solution Workbench.slnx
```

Validate canonical JSON artifacts against the schema snapshot pinned into Workbench:

```powershell
pwsh -File scripts/Validate-SpecTraceJson.ps1 -RepoRoot C:\path\to\repo -Profiles core,traceable
```

Run unit tests only:

```bash
dotnet test --project tests/Workbench.Tests/Workbench.Tests.csproj
```

Run integration tests:

```bash
dotnet test --project tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

Produce raw quality evidence in the standard repo locations:

```powershell
pwsh -File scripts/testing/run-quality-evidence.ps1
```

On macOS or Linux:

```bash
bash ./scripts/testing/run-quality-evidence.sh
```

Run GitHub CLI-dependent integration tests:

```bash
WORKBENCH_RUN_GH_TESTS=1 dotnet test --project tests/Workbench.IntegrationTests/Workbench.IntegrationTests.csproj
```

Pack the .NET tool:

```bash
dotnet pack src/Workbench/Workbench.csproj -c Release
```

Publish a self-contained single-file binary:

```bash
dotnet publish src/Workbench/Workbench.csproj -c Release -r osx-arm64
```

Replace the runtime identifier with your target (e.g., `win-x64`, `linux-x64`).

Verification steps:

```bash
ls src/Workbench/bin/Release/net10.0/osx-arm64/publish
./src/Workbench/bin/Release/net10.0/osx-arm64/publish/workbench --help
```

Expected warnings:
- None. Treat publish-time warnings as regressions.

## Documentation and contracts

- Overview: [`overview.md`](overview.md)
- Authoring guide: [`authoring.md`](authoring.md)
- Layout guide: [`layout.md`](layout.md)
- Requirements: `specs/requirements/`
- Architecture: `specs/architecture/`
- Verification artifacts: `specs/verification/`
- Work items: `specs/work-items/`
- Templates: `specs/templates/`
- Schemas: `specs/schemas/` and the pinned SpecTrace model embedded in Workbench
- Canonical CLI help snapshot: [`specs/generated/commands.md`](specs/generated/commands.md)
- Quality intent contract: [`quality/testing-intent.yaml`](quality/testing-intent.yaml)

## Quality evidence

Workbench quality evidence is advisory in this repo. `workbench quality sync`
normalizes raw test and coverage evidence into `artifacts/quality/testing/`.
`workbench quality attest` produces a read-only snapshot in
`artifacts/quality/attestation/` that rolls up requirement coverage, trace
completeness, direct refs, work-item status, verification status, and evidence
health. Neither command mutates canonical trace or turns direct refs into
canonical downstream edges for canonical artifacts.

Happy path:

```powershell
dotnet tool restore
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage
dotnet tool run workbench quality show
dotnet tool run workbench quality attest
```

Path conventions:

- Authored intent: [`quality/testing-intent.yaml`](quality/testing-intent.yaml)
- Attestation defaults: [`quality/attestation.yaml`](quality/attestation.yaml)
- Raw test evidence: `artifacts/quality/raw/test-results/*.trx`
- Raw coverage evidence: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated quality artifacts: `artifacts/quality/testing/`
- Generated attestation artifacts: `artifacts/quality/attestation/`

Generated artifacts under `artifacts/quality/testing/` are derived outputs. Do
not hand-edit them, and do not treat them as canonical `Verified By` coverage
unless you explicitly project them into a verification artifact.

Generated attestation artifacts under `artifacts/quality/attestation/` are
derived outputs too. They report the current repository snapshot; they do not
replace authored requirements or canonical trace.

## Voice commands

- `workbench voice workitem` records audio, transcribes it, and generates a
  work item.
- `workbench voice doc --type <specification|architecture|verification|work_item> [--out <path>] [--title "<...>"]` records audio, transcribes it, and generates a canonical artifact payload.
- While recording, press ENTER to stop or ESC to cancel.

Requirements:
- Set `OPENAI_API_KEY` (or `WORKBENCH_AI_OPENAI_KEY`) for transcription.
- macOS: allow the terminal (or the built binary) in System Settings -> Privacy & Security -> Microphone.

Optional configuration:
- `WORKBENCH_AI_TRANSCRIPTION_MODEL` (default: `gpt-4o-mini-transcribe`)
- `WORKBENCH_AI_TRANSCRIPTION_LANGUAGE` (e.g., `en`)
- `WORKBENCH_VOICE_MAX_DURATION_SECONDS` (default: `240`)

Smoke test:
- Run `workbench voice workitem`, speak a short phrase, press ENTER, and confirm a work item file is created.

## Recording visualization

Workbench shows a small level meter/equalizer in the recording dialog.

Optional knobs (env vars):
- `WORKBENCH_VOICE_VIZ_BANDS` (default: `12`)
- `WORKBENCH_VOICE_VIZ_UPDATE_HZ` (default: `20`)
- `WORKBENCH_VOICE_VIZ_FFT_SIZE` (default: `1024`)
- `WORKBENCH_VOICE_VIZ_LEVEL_BOOST` (default: `1.6`)
- `WORKBENCH_VOICE_VIZ_SPECTRUM` (default: `true`)

## CI

GitHub Actions builds and tests on `ubuntu-latest`, `windows-latest`, and
`macos-latest` with .NET `10.0.x`:

```bash
dotnet build Workbench.slnx
dotnet test --solution Workbench.slnx
```

## Contributing

- [Contribution guide](CONTRIBUTING.md)
- [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security policy](SECURITY.md)
