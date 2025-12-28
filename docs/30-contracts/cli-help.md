---
workbench:
  type: doc
  workItems: []
  codeRefs: []
owner: platform
status: active
updated: 2025-12-27
---

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
  - Use `--json` for machine-readable output.
  - Example: `workbench doctor`

- `workbench init [--force] [--skip-wizard] [--non-interactive] [--front-matter] [--configure-openai] [--credential-store <local|external|skip>] [--credential-path <path>] [--openai-provider <openai|none>] [--openai-key <key>] [--openai-model <model>]`
  - Guided setup for scaffolding, front matter guidance, and OpenAI configuration.
  - Runs the `run` wizard afterward unless `--skip-wizard` is set.
  - Example: `workbench init --skip-wizard`

- `workbench run`
  - Launch the interactive wizard for common document and work item actions.
  - Example: `workbench run`

- `workbench sync [--items] [--docs] [--nav] [--issues <true|false>] [--include-done] [--force] [--dry-run] [--prefer <local|github>]`
  - Run the full repo sync (work items, docs/front matter, and navigation) in order. When no step flags are provided, runs all.
  - Example: `workbench sync --dry-run`

- `workbench scaffold [--force]`
  - Create the default folder structure, templates, and config.
  - Example: `workbench scaffold`

- `workbench config show`
  - Print effective config (defaults + repo config + CLI overrides).
  - Example: `workbench config show --format json`

- `workbench item new --type <bug|task|spike> --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]`
  - Create a new work item in `work/items` using templates and ID allocation.
  - Example: `workbench item new --type task --title "Add promote command"`
- Status values: `draft`, `ready`, `in-progress`, `blocked`, `done`, `dropped`.
- `workbench item import --issue <id|url...> [--type <bug|task|spike>] [--status <...>] [--priority <...>] [--owner <...>]`
  - Import GitHub issues into work items, linking related PRs when available.
  - Example: `workbench item import --issue 42 --issue https://github.com/org/repo/issues/18`
- Status values: `draft`, `ready`, `in-progress`, `blocked`, `done`, `dropped`.

- `workbench item sync [--id <ID...>] [--issue <id|url...>] [--prefer <local|github>] [--dry-run]`
  - Sync work items with GitHub issues and branches (two-way, no deletes). Branches are only created when listed in `related.branches`. Defaults to pushing local content to GitHub unless `--prefer github` is set for ID-scoped sync.
  - Example: `workbench item sync --dry-run`

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
- Status values: `draft`, `ready`, `in-progress`, `blocked`, `done`, `dropped`.

- `workbench item show <ID>`
  - Show metadata and resolved path for an item.
  - Example: `workbench item show TASK-0042`

- `workbench item status <ID> <status> [--note "<...>"]`
  - Update status and updated date. Optionally append a note.
  - Example: `workbench item status TASK-0042 in-progress --note "started implementation"`
- Status values: `draft`, `ready`, `in-progress`, `blocked`, `done`, `dropped`.

- `workbench item close <ID> [--move]`
  - Set status to `done`; optionally move the file to `work/done`.
  - Example: `workbench item close TASK-0042 --move`

- `workbench item move <ID> --to <path>`
  - Move a work item file and update inbound links to the old path where possible.
  - Example: `workbench item move TASK-0042 --to work/done/TASK-0042-add-promotion-workflow.md`

- `workbench item rename <ID> --title "<...>"`
  - Regenerate slug from title, rename the file, and update inbound links.
  - Example: `workbench item rename TASK-0042 --title "Finalize promotion workflow"`

- `workbench item link <ID> [--spec <path...>] [--adr <path...>] [--file <path...>] [--pr <url...>] [--issue <id...>] [--dry-run]`
  - Add spec/ADR/file/PR/issue links to a work item and update doc backlinks when applicable.
  - Example: `workbench item link TASK-0042 --spec /docs/10-product/payment-flow.md --pr https://github.com/org/repo/pull/12`

- `workbench item unlink <ID> [--spec <path...>] [--adr <path...>] [--file <path...>] [--pr <url...>] [--issue <id...>] [--dry-run]`
  - Remove spec/ADR/file/PR/issue links from a work item and update doc backlinks when applicable.
  - Example: `workbench item unlink TASK-0042 --adr /docs/40-decisions/2025-01-01-audit-logs.md`

- `workbench board regen`
  - Regenerate `work/WORKBOARD.md`.
  - Example: `workbench board regen`

- `workbench doc new --type <spec|adr|doc|runbook|guide> --title "<...>" [--path <...>] [--work-item <ID...>] [--code-ref <ref...>] [--force]`
  - Create a documentation file with Workbench front matter and optional backlinks.
  - Example: `workbench doc new --type spec --title "Payment flow" --work-item TASK-0042`

- `workbench doc sync [--all] [--issues] [--include-done] [--dry-run]`
  - Sync doc/work item backlinks. `--all` adds Workbench front matter to all docs; `--issues` syncs GitHub issue links; `--include-done` includes done/dropped items; `--dry-run` reports changes without writing.
  - Example: `workbench doc sync --all --issues --dry-run`

- `workbench nav sync [--issues <true|false>] [--include-done] [--force] [--dry-run]`
  - Sync doc/work item backlinks and update navigation indexes. Defaults to syncing issue links; set `--issues false` to skip GitHub lookups. `--force` rewrites index sections even if they are unchanged.
  - Example: `workbench nav sync --include-done --issues false --force`

- `workbench doc summarize [--staged] [--path <path...>] [--dry-run] [--update-index]`
  - Summarize markdown diffs using AI and append `workbench.changeNotes` entries.
  - Example: `workbench doc summarize --staged --update-index`

- `workbench spec new --title "<...>" [--path <...>] [--work-item <ID...>] [--code-ref <ref...>] [--force]`
  - Create a spec document and auto-link work items.
  - Example: `workbench spec new --title "Access model" --work-item TASK-0100`

- `workbench spec link --path <...> --work-item <ID...> [--dry-run]`
  - Link a spec document to work items.
  - Example: `workbench spec link --path /docs/10-product/access-model.md --work-item TASK-0100`

- `workbench spec unlink --path <...> --work-item <ID...> [--dry-run]`
  - Unlink a spec document from work items.
  - Example: `workbench spec unlink --path /docs/10-product/access-model.md --work-item TASK-0100`

- `workbench adr new --title "<...>" [--path <...>] [--work-item <ID...>] [--code-ref <ref...>] [--force]`
  - Create an ADR document and auto-link work items.
  - Example: `workbench adr new --title "Persist audit logs" --work-item TASK-0123`

- `workbench adr link --path <...> --work-item <ID...> [--dry-run]`
  - Link an ADR document to work items.
  - Example: `workbench adr link --path /docs/40-decisions/2025-01-01-audit-logs.md --work-item TASK-0123`

- `workbench adr unlink --path <...> --work-item <ID...> [--dry-run]`
  - Unlink an ADR document from work items.
  - Example: `workbench adr unlink --path /docs/40-decisions/2025-01-01-audit-logs.md --work-item TASK-0123`
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
- "add task/bug/spike" is shorthand for item creation.
- "verify all work" maps to `workbench validate` (use `--strict` for CI).
- "create pr" maps to `workbench pr create <ID>`.

Dependencies:
- Commands that read or write work items require a git repo.
- `promote` requires git.
- `pr create` and `promote --pr` require `gh` to be installed and authenticated.
