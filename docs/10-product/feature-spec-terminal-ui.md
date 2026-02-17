---
workbench:
  type: spec
  workItems:
    - TASK-0005
    - TASK-0006
    - TASK-0007
  codeRefs: []
  pathHistory: []
  path: /docs/10-product/feature-spec-terminal-ui.md
owner: platform
status: draft
updated: 2025-12-30
---

# Feature Spec: Terminal UI Mode

## Summary
Add an interactive terminal UI mode to the Workbench CLI, exposed as a subcommand (e.g., `workbench tui`), while keeping a single published executable. The TUI will reuse the existing service layer and work item/document model without changing data formats.

## Goals
- Provide a guided, interactive interface for common work item and doc workflows.
- Keep a single executable for CLI and TUI usage.
- Avoid duplicating business logic by reusing the existing service layer.
- Keep the CLI as the source of truth for data mutation and validation.

## Non-goals
- No web UI or desktop GUI.
- No reformatting of existing work item/doc markdown structures.
- No advanced visualization (kanban boards, charts) in the initial TUI.
- No new external storage system; file system remains the canonical store.

## User stories / scenarios
- As a user, I can open a TUI to browse work items and see details.
- As a user, I can create, rename, move, or close a work item from the TUI.
- As a user, I can open linked docs from the TUI and sync backlinks.
- As a user, I can exit the TUI and continue with normal CLI commands.

## Requirements
- New subcommand: `workbench tui` (and `workbench t` alias if desired).
- TUI runs in-terminal using Terminal.Gui.
- TUI uses existing services for reads and writes (no duplicated parsing or validation logic).
- TUI exposes a minimal set of workflows:
  - List work items (filters by status or prefix).
  - Show work item details.
  - Create work item (select template, collect fields).
  - Update work item status and title.
  - Open linked docs or create a doc from a template.
  - Run sync/validate operations with progress feedback.
- TUI displays the last CLI command invoked for each action.
- TUI supports a global dry-run toggle and marks dry-run outputs clearly.
- Error handling surfaces CLI validation messages in UI-friendly dialogs.
- TUI exit must restore terminal state cleanly.

## Scope
- Read and write work items and docs in the current repo using existing services.
- Surface a bounded set of actions; unsupported commands fall back to the CLI.
- Provide a consistent command preview for all mutations and external calls.
- Keep dry-run behavior aligned with existing CLI flags.

## Workflows
### Browse and inspect work items
1. Enter TUI and load items from `docs/70-work/items`.
2. Filter by status or prefix; list updates without leaving the view.
3. Select an item to view metadata, related links, and latest notes.

### Create a work item
1. Select type and template.
2. Enter title, owner, priority, and initial status.
3. Preview the CLI command and resulting file path.
4. Execute and show confirmation; open the created item if requested.

### Update status/title
1. Select item.
2. Edit status/title fields.
3. Preview the CLI command and the front matter change.
4. Execute and show any validation warnings.

### Sync/validate
1. Choose sync or validate.
2. Preview the command with flags (including dry-run).
3. Execute and show a streaming log with a final summary.

## UX requirements
- Layout: left navigation list, right detail pane, bottom action hints.
- Keyboard-first navigation (arrows, Enter, Esc, common shortcuts).
- Status bar shows repo, current filter, and last action.
- Dialogs are minimal and consistent with CLI terminology.
- Use consistent copy for statuses and types (match CLI enum values).

## Command preview and dry-run
- Preview line is always visible for actions that would mutate state.
- Preview includes the exact CLI subcommand and flags that will run.
- Dry-run toggle is global and persists across TUI views.
- Dry-run state is shown in the status bar and prepended to preview.
- Dry-run results are labeled and do not change files.

## UX notes
- Keep dialogs minimal; use standard form prompts with validation.
- Provide a visible dry-run indicator when enabled.

## Dependencies
- Terminal.Gui for the UI layer.
- Existing services in `src/Workbench` (WorkItemService, DocService, ConfigService, etc).
- A shared command or handler layer to invoke mutations consistently.

## Architecture approach
- Split into projects but publish as one executable:
  - `Workbench.Core` (new): domain model, parsing, validation, service layer.
  - `Workbench.Cli` (existing or new): command handlers and CLI wiring.
  - `Workbench.Tui` (new): TUI shell and view models, calls core services.
- `Workbench` console app remains the entrypoint and dispatches to CLI or TUI.
- Use `dotnet publish` single-file mode to deliver one binary.

## Risks and mitigations
- Risk: UI logic duplicates CLI logic.
  - Mitigation: move shared logic into `Workbench.Core` and keep a thin CLI/TUI.
- Risk: TUI introduces terminal state bugs.
  - Mitigation: standardize start/stop lifecycle and test on common shells.
- Risk: Increased build complexity with multiple projects.
  - Mitigation: keep public APIs minimal and document build/publish steps.

## Related work items
- /docs/70-work/items/TASK-0005-plan-terminal-ui-mode.md

## Related ADRs
- /docs/40-decisions/ADR-2025-12-30-terminal-ui-mode-in-cli-executable.md

## Open questions
- Which TUI framework best balances maturity, design control, and ease of testing?
- Should TUI commands call CLI handlers directly, or should both call a shared command service?
- Do we want `workbench tui` to allow invoking arbitrary CLI commands, or only the supported set?
