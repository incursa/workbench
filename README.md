# Workbench

Workbench is a .NET CLI for repo-native work: specs, decisions, work items,
validation, and generated navigation that all live in source control. Local
Markdown plus JSON contracts are canonical; GitHub is an optional sync and
mirror layer, not the primary system of record.

## Operating model

- Hand-author specs, ADRs, and work items as Markdown under `docs/`.
- Let Workbench maintain front matter, backlinks, and the generated sections
  inside the repo READMEs.
- Use GitHub issues, branches, and PRs as linked execution artifacts around the
  local Markdown record.

## Happy path

1. Use `workbench guide` for the human-friendly entry point, or go straight to
   `workbench item new` / `workbench doc new`.
2. Use `workbench item edit` for structured updates to work-item title,
   summary, acceptance criteria, and notes; edit the Markdown directly when you
   need broader freeform changes.
3. Use `workbench item link` to connect specs, ADRs, files, PRs, or issues, and
   `workbench promote` when you want branch + commit scaffolding in one step.
4. Refresh generated views with `workbench nav sync` and run
   `workbench validate` before review or automation.
5. Agents should prefer `workbench llm help` and `--format json`.

## Sync model

- Use `workbench sync` for the common repo-wide happy path. It is the umbrella command and runs the lower-level sync stages.
- Use `workbench item sync` when you specifically need to reconcile local work items with GitHub issues or branch state.
- Use `workbench doc sync` when you need to repair or refresh doc front matter and doc/work-item backlinks without rebuilding indexes.
- Use `workbench nav sync` when you need to rebuild derived docs indexes, repo indexes, or the workboard. It also syncs links first unless that work already ran via `workbench sync --docs --nav`.
- Use `workbench board regen` only when you want the narrowest workboard-only refresh.

## Repository map

- `src/Workbench`: CLI source code.
- `tests/`: automated tests.
- `docs/`: product, architecture, contracts, decisions, and runbooks.
- `docs/70-work/`: active and completed work items plus templates.
- `assets/`: static assets used by docs or tooling.
- `artifacts/`: build outputs and local artifacts.
- `testdata/`: fixtures for parsing and validation tests.

## Requirements

- .NET SDK `10.0.100` (see `global.json`).
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

Publish a native binary (AOT):

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
- None. Treat any trimming/AOT warnings (IL2026/IL3050) as regressions.

## Documentation and contracts

- Docs overview: `docs/README.md`
- Work items: `docs/70-work/README.md`
- Operating model ADR: `docs/40-decisions/ADR-2026-03-07-repo-native-operating-model.md`
- CLI help: `docs/30-contracts/cli-help.md`
- Schemas: `docs/30-contracts/`
- Testing intent contract: `docs/30-contracts/test-gate.contract.yaml`

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

- Authored intent: `docs/30-contracts/test-gate.contract.yaml`
- Raw test evidence: `artifacts/quality/raw/test-results/*.trx`
- Raw coverage evidence: `artifacts/quality/raw/coverage/*.cobertura.xml`
- Generated quality artifacts: `artifacts/quality/testing/`

Generated artifacts under `artifacts/quality/testing/` are derived outputs. Do not hand-edit them.

## Voice commands

- `workbench voice workitem` records audio, transcribes it, and generates a work item.
- `workbench voice doc --type <spec|adr|doc|runbook|guide> [--out <path>] [--title "<...>"]` records audio, transcribes it, and generates a doc with YAML front matter.
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

## Navigation

Generated by `workbench nav sync`.

<!-- workbench:root-index:start -->

### Quick links
- [Docs index](docs/README.md)
- [Work index](docs/70-work/README.md)

### Work item stats
| Metric | Count |
| --- | --- |
| Open | 17 |
| Closed | 3 |
| Total | 20 |

| Status | Count |
| --- | --- |
| 🟡 draft | 16 |
| 🟢 ready | 0 |
| 🔵 in-progress | 1 |
| 🟥 blocked | 0 |
| ✅ done | 3 |
| 🚫 dropped | 0 |
<!-- workbench:root-index:end -->

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
