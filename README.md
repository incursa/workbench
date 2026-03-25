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

## Operating model

- Keep canonical requirements in `specs/requirements/`.
- Keep architecture docs in `specs/architecture/`.
- Keep work items in `specs/work-items/`.
- Keep verification artifacts in `specs/verification/`.
- Keep generated repository views under `specs/generated/`.
- Keep canonical templates under `specs/templates/`.
- Keep canonical schemas under `specs/schemas/`.
- Keep the quality intent contract in [`quality/testing-intent.yaml`](quality/testing-intent.yaml).
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
   `workbench validate` before review or automation.
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
- Schemas: `specs/schemas/`
- Canonical CLI help snapshot: [`specs/generated/commands.md`](specs/generated/commands.md)
- Quality intent contract: [`quality/testing-intent.yaml`](quality/testing-intent.yaml)

## Quality evidence

Workbench quality evidence is advisory in this repo. It summarizes authored test intent plus observed test and coverage artifacts, but it does not introduce a merge gate.

Happy path:

```powershell
dotnet tool restore
pwsh -File scripts/testing/run-quality-evidence.ps1
dotnet tool run workbench quality sync --results artifacts/quality/raw/test-results --coverage artifacts/quality/raw/coverage
dotnet tool run workbench quality show
```

Path conventions:

- Authored intent: [`quality/testing-intent.yaml`](quality/testing-intent.yaml)
- Raw test evidence: `artifacts/quality/raw/test-results/*.trx`
- Raw coverage evidence: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated quality artifacts: `artifacts/quality/testing/`

Generated artifacts under `artifacts/quality/testing/` are derived outputs. Do not hand-edit them.

## Voice commands

- `workbench voice workitem` records audio, transcribes it, and generates a
  work item.
- `workbench voice doc --type <specification|architecture|verification|work_item> [--out <path>] [--title "<...>"]` records audio, transcribes it, and generates a canonical artifact with front matter.
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
