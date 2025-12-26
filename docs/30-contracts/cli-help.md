# Workbench CLI Help (v0.1)

Usage:
```
workbench <command> [options]
```

Global options:
- `--repo <path>`: target repo (defaults to current dir)
- `--format table|json`: output format (default: table)
- `--no-color`: disable colored output
- `--quiet`: suppress non-error output

Config:
- Repo config path: `.workbench/config.json`

Environment overrides:
- `WORKBENCH_REPO`: default repo path (overrides current dir)
- `WORKBENCH_FORMAT`: default format (`table` or `json`)
- `WORKBENCH_NO_COLOR`: set to `1` to disable color
- `WORKBENCH_QUIET`: set to `1` to suppress non-error output

Exit codes:
- `0`: success, no warnings
- `1`: success with warnings (validate/doctor only)
- `2`: command failed due to errors

Common error codes:
- `WB001`: not a git repo / repo not found
- `WB002`: git not installed / not callable
- `WB010`: config parse or schema error
- `WB020`: work item not found by ID
- `WB030`: front matter schema invalid
- `WB040`: validation error (broken link, duplicate ID, etc.)
- `WB050`: git command failed
- `WB060`: GitHub (gh) command failed

Commands:

- `workbench version`
  - Print CLI version.
  - Example: `workbench version`

- `workbench doctor`
  - Check git, config, and expected paths.
  - Includes `gh` availability/auth checks; unauthenticated `gh` reports a warning.
  - Example: `workbench doctor`

- `workbench init [--force]`
  - Alias for `workbench scaffold`.
  - Example: `workbench init --force`

- `workbench scaffold [--force]`
  - Create the default folder structure, templates, and config.
  - Example: `workbench scaffold`

- `workbench config show`
  - Print effective config (defaults + repo config + CLI overrides).
  - Example: `workbench config show --format json`

- `workbench item new --type <bug|task|spike> --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]`
  - Create a new work item in `work/items` using templates and ID allocation.
  - Example: `workbench item new --type task --title "Add promote command"`

- `workbench add task --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]`
  - Alias for `workbench item new --type task`.
  - Example: `workbench add task --title "Define link validation"`

- `workbench add bug --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]`
  - Alias for `workbench item new --type bug`.
  - Example: `workbench add bug --title "Fix ID allocation"`

- `workbench add spike --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]`
  - Alias for `workbench item new --type spike`.
  - Example: `workbench add spike --title "Evaluate PR workflow"`

- `workbench item list [--type <...>] [--status <...>] [--include-done]`
  - List work items. Use `--include-done` to include `work/done`.
  - Example: `workbench item list --status ready`

- `workbench item show <ID>`
  - Show metadata and resolved path for an item.
  - Example: `workbench item show TASK-0042`

- `workbench item status <ID> <status> [--note "<...>"]`
  - Update status and updated date. Optionally append a note.
  - Example: `workbench item status TASK-0042 in-progress --note "started implementation"`

- `workbench item close <ID> [--move]`
  - Set status to `done`; optionally move the file to `work/done`.
  - Example: `workbench item close TASK-0042 --move`

- `workbench item move <ID> --to <path>`
  - Move a work item file and update inbound links to the old path where possible.
  - Example: `workbench item move TASK-0042 --to work/done/TASK-0042-add-promotion-workflow.md`

- `workbench item rename <ID> --title "<...>"`
  - Regenerate slug from title, rename the file, and update inbound links.
  - Example: `workbench item rename TASK-0042 --title "Finalize promotion workflow"`

- `workbench board regen`
  - Regenerate `work/WORKBOARD.md`.
  - Example: `workbench board regen`

- `workbench promote --type <...> --title "<...>" [--push] [--start] [--pr] [--base <branch>] [--draft|--no-draft]`
  - Create a work item, branch, and commit in one step; optionally create a PR.
  - Example: `workbench promote --type task --title "Add validate command" --start --pr --draft`

- `workbench pr create <ID> [--base <branch>] [--draft] [--fill]`
  - Create a GitHub PR via `gh` and backlink the PR URL.
  - Example: `workbench pr create TASK-0042 --draft --fill`

- `workbench create pr <ID> [--base <branch>] [--draft] [--fill]`
  - Alias for `workbench pr create`.

- `workbench validate [--strict]`
  - Validate work items, links, and schemas. `--strict` treats warnings as errors.
  - Example: `workbench validate --strict`

- `workbench verify [--strict]`
  - Alias for `workbench validate`.

Aliases and intent:
- "init" maps to scaffolding the repo.
- "add task/bug/spike" is shorthand for item creation.
- "verify all work" maps to `workbench validate` (use `--strict` for CI).
- "create pr" maps to `workbench pr create <ID>`.

Dependencies:
- Commands that read or write work items require a git repo.
- `promote` requires git.
- `pr create` and `promote --pr` require `gh` to be installed and authenticated.
