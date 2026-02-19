---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/00-overview/workbench-spec.md"
  path: /docs/00-overview/workbench-spec.md
owner: platform
status: active
updated: 2025-12-27
---

# Incursa Workbench - Specification (v0.1)

## Summary
Workbench is a standalone .NET CLI that standardizes a Markdown-first engineering workflow in any git repository by managing:
- work items as Markdown files with YAML front matter
- repo scaffolding for /work and /docs
- validation of links/metadata/conventions
- promotion workflow: create branch + commit a new work item
- GitHub PR creation (via provider, Octokit by default) and backlinking PR URLs into the work item

Workbench is designed to be repo-agnostic and operate on the current directory or a provided path.

---

## Problem statement
Work gets scattered across:
- issue trackers
- ad-hoc docs
- internal notes
- PR descriptions

Result: missing context, stale requirements, unclear "done", and poor automation.

Workbench makes the repo the source of truth for:
- execution (/work)
- design/decisions (/docs)
while still allowing external systems as intake.

---

## Goals
1) Make "promoted work" reproducible:
   - create a work item file
   - create a branch named from that work item
   - commit it immediately

2) Make work items machine-readable and human-usable:
   - YAML front matter schema
   - stable templates
   - consistent naming

3) Keep docs and work linked:
   - work items reference specs/ADRs
   - PRs reference work items
   - optional backlinks stored in front matter

4) Enable automation:
   - JSON output option
   - stable exit codes
   - deterministic validation rules

5) Keep dependencies light:
   - use git CLI by default and a GitHub provider (Octokit default)
   - no server required

---

## Non-goals
- Replacing Jira/GitHub Issues as an inbound support/feedback system
- Implementing a full project management UI
- Network-heavy validation (e.g., checking PR URLs exist via API) by default
- Enforcing a specific architecture, framework, or style guide in the target repo

---

## Target users
- Solo developers and small teams that want consistent workflow
- Teams using Codex/agents that require everything in source control
- Repos that want "work as code" without adopting a complex platform

---

## Terms
- Workspace: a git repo (detected automatically).
- Work item: a Markdown file representing a unit of work (bug/task/spike).
- Promotion: creating the work item on a new branch with an initial commit.
- Spec: documentation in /docs that describes behavior/requirements.
- ADR: architecture decision record, stored in /docs/40-decisions.

---

## Repository conventions (scaffolded)
Workbench supports configurable paths, but defaults to:

```
/docs/
  README.md
  00-overview/
  10-product/
  20-architecture/
  30-contracts/
  40-decisions/
  50-runbooks/
  60-tracking/

/docs/70-work/
  README.md
  /items/
  /done/                (optional)
  /templates/
    work-item.bug.md
    work-item.task.md
    work-item.spike.md

/.workbench/config.json  (configuration file at repo root)
```

Scaffolding rules:
- Never overwrite existing files unless --force.
- Templates are copied into /docs/70-work/templates.
- Minimal README files explain "what goes here".
- The workboard lives inside /docs/70-work/README.md.

---

## Work item format

### File location and naming
Default:
- Active items: /docs/70-work/items/<ID>-<slug>.md
- Done items: /docs/70-work/done/<ID>-<slug>.md (optional)

<slug> is derived from title:
- lowercase
- spaces -> -
- remove punctuation except -
- collapse multiple -

### ID allocation
Workbench determines the next ID by scanning existing items in `docs/70-work/items` and `docs/70-work/done`.
Allocation rules:
- IDs are per type (bug/task/spike) and monotonically increase.
- Width is padded to the configured `ids.width` (default: 4).
- Gaps are allowed; the next ID is `max(existing)+1`.

### Template tokens
Templates in `docs/70-work/templates` are copied verbatim, then tokens are replaced:
- `<title>` becomes the title passed to `workbench item new`
- `0000-00-00` becomes the current date (UTC, YYYY-MM-DD)
- `BUG-0000` / `TASK-0000` / `SPIKE-0000` replaced with the allocated ID

### File encoding and line endings
- All generated files are UTF-8 with LF line endings.
- Workbench preserves the existing body content when updating front matter.

### YAML front matter
Work items must start with YAML front matter delimited by --- and ---.

### Schema contract
Front matter must conform to the JSON Schema at:
`/docs/30-contracts/work-item.schema.json`

#### Required fields
- id (string): BUG-0001, TASK-0023, SPIKE-0004
- type (enum): bug | task | spike
- status (enum): draft | ready | in-progress | blocked | done | dropped
- created (ISO date): YYYY-MM-DD

#### Optional fields
- title (string): stored for convenience; filename remains canonical
- priority (enum): low | medium | high | critical
- owner (string|null)
- updated (ISO date)
- tags (string array)
- related (object):
  - specs (string array): repo-relative paths
  - adrs (string array): repo-relative paths
  - files (string array): repo-relative paths described by this work item
  - prs (string array): URLs
  - issues (string array): URLs or external IDs
  - branches (string array): local branch names

Two-way links:
- Any file listed in related.files must include a backlink to the work item ID.

#### Example
```md
---
id: TASK-0042
type: task
status: ready
priority: high
owner: null
created: 2025-12-25
updated: null
tags: [docs, workflow]
related:
  specs:
    - /docs/10-product/features/feature.work-items.md
  adrs: []
  files: []
  prs: []
  issues: []
  branches: []
---

# TASK-0042 - Add promotion workflow

## Summary
...

## Acceptance criteria
- ...
```

---

## Doc front matter

Docs use YAML front matter aligned with `/docs/30-contracts/doc.schema.json`. Workbench keeps path metadata so moved files can be detected and links updated.

### Workbench fields
- workbench.type: spec | adr | doc | runbook | guide
- workbench.workItems: linked work item IDs
- workbench.codeRefs: related code references
- workbench.path: current repo-relative path (e.g. /docs/10-product/onboarding.md)
- workbench.pathHistory: prior repo-relative paths used to repair links after moves
- status: active | draft | legacy (recommended for imported docs)

### Body conventions (template-driven)
Each type has a template. At minimum:
- Title H1
- Summary
- Acceptance criteria (bulleted)

---

## Configuration

### Config file
Stored at: /.workbench/config.json (repo root)

### Config schema
The config must conform to:
`/docs/30-contracts/workbench-config.schema.json`

### Defaults
Workbench works without config (defaults applied), but writes config on scaffold.

### Config shape (v0.1)
```json
{
  "paths": {
    "docsRoot": "docs",
    "workRoot": "docs/70-work",
    "itemsDir": "docs/70-work/items",
    "doneDir": "docs/70-work/done",
    "templatesDir": "docs/70-work/templates",
    "workboardFile": "docs/70-work/README.md"
  },
  "ids": {
    "width": 4,
    "prefixes": {
      "bug": "BUG",
      "task": "TASK",
      "spike": "SPIKE"
    }
  },
  "git": {
    "branchPattern": "work/{id}-{slug}",
    "commitMessagePattern": "Promote {id}: {title}",
    "defaultBaseBranch": "main",
    "requireCleanWorkingTree": true
  },
  "github": {
    "provider": "octokit",
    "defaultDraft": false
  }
}
```

### Config precedence
When multiple sources are present, settings are resolved in this order:
1. Built-in defaults
2. Repo config file (/.workbench/config.json)
3. CLI flags (highest precedence)

---

## CLI behavior

### Global flags (all commands)
- --repo <path>: target repo (defaults to current dir)
- --format table|json (default: table)
- --no-color
- --quiet

Environment overrides:
- WORKBENCH_REPO: default repo path (overrides current dir)
- WORKBENCH_FORMAT: default format (table or json)
- WORKBENCH_NO_COLOR: set to 1 to disable color
- WORKBENCH_QUIET: set to 1 to suppress non-error output

### Commands (v0.1)

#### workbench version
Print version.

#### workbench doctor
Checks prerequisites and workspace:
- git installed and repo detected
- config detected (warn if missing, error if invalid)
- paths exist (warn if not scaffolded)
Use `--json` for machine-readable output.

#### workbench init [--force] [--skip-guide] [--non-interactive] [--front-matter] [--configure-openai] [...]
Guided setup for scaffolding, front matter guidance, and OpenAI configuration.
Runs the `guide` flow afterward unless `--skip-guide` is set.

#### workbench guide
Launches the interactive guide for common document/work item actions.

#### workbench sync [--items] [--docs] [--nav] [--issues <true|false>] [--import-issues] [--include-done] [--force] [--dry-run] [--prefer <local|github>]
Runs the full repository sync in order: work items ↔ GitHub issues, doc backlinks/front matter, then navigation indexes. When no step flags are provided, runs all.

#### workbench migrate coherent-v1 [--dry-run]
Runs the coherence migration: enforces folder/status placement for work items,
normalizes links, and regenerates navigation/workboard outputs.

#### workbench scaffold [--force]
Creates the default folder structure, templates, and config.

#### workbench config show
Print effective config (defaults + repo config).

#### workbench config set --path <path> --value "<...>" [--json]
Updates a config value by dot-path and writes to /.workbench/config.json.

#### workbench config credentials set --key <KEY> --value "<...>" [--path <path>]
Writes or updates a credentials.env entry (defaults to /.workbench/credentials.env).

#### workbench config credentials unset --key <KEY> [--path <path>]
Removes a credentials.env entry.

#### workbench item new --type <bug|task|spike> --title "<...>" [--status <...>] [--priority <...>] [--owner <...>]
Creates a new work item file in /docs/70-work/items using templates and ID allocation rules.

#### workbench item generate --prompt "<...>" [--type <bug|task|spike>] [--status <...>] [--priority <...>] [--owner <...>]
Generates a work item draft with AI from freeform text and creates it in /docs/70-work/items.

#### workbench item list [--type ...] [--status ...] [--include-done]
Lists items from active and optionally includes done items.

#### workbench item show <ID>
Prints the resolved file path and renders key metadata.

#### workbench item status <ID> <status> [--note "<...>"]
Updates front matter status and updated. Optional note appended to a Notes section.

#### workbench item close <ID> [--move]
Sets status to done and optionally moves file to done dir.

#### workbench item delete <ID> [--keep-links]
Deletes a work item file and removes doc backlinks unless `--keep-links` is set.

#### workbench doc new --type <spec|adr|doc|runbook|guide> --title "<...>" [--path <...>] [--work-item <ID...>] [--code-ref <ref...>] [--force]
Creates a documentation file with Workbench front matter and optional backlinks.

#### workbench doc delete --path <...> [--keep-links]
Deletes a documentation file and removes work item links unless `--keep-links` is set.

#### workbench doc link --type <spec|adr> --path <...> --work-item <ID...> [--dry-run]
Links a spec/ADR doc to one or more work items.

#### workbench doc unlink --type <spec|adr> --path <...> --work-item <ID...> [--dry-run]
Unlinks a spec/ADR doc from one or more work items.

#### workbench doc sync [--all] [--issues] [--include-done] [--dry-run]
Syncs doc/work item backlinks and front matter. Adds front matter to all docs by default; pass `--all false` to limit to referenced docs.

#### workbench doc summarize [--staged] [--path <path...>] [--dry-run] [--update-index]
Summarizes markdown diffs with AI and appends change notes.

#### workbench board regen
Regenerates the workboard section in /docs/70-work/README.md with sections based on status:
- Now: in-progress
- Next: ready
- Blocked
- Draft

Workboard format:
- Markdown with fixed H2 sections in the order above.
- Each section contains a bulleted list of items: `- <ID> - <title> (<path>)`.
- Items are sorted by ID ascending.

#### workbench promote --type <...> --title "<...>" [--push] [--start] [--pr] [--base <branch>] [--draft|--no-draft]
Promotion workflow:
1. ensure workspace
2. ensure clean tree (configurable)
3. create work item
4. create and checkout new branch using branchPattern
5. commit the new file using commitMessagePattern
6. optional push

--start additionally sets status to in-progress.
--pr creates a GitHub PR and backfills related.prs on success.
--draft and --no-draft override the default draft behavior.

#### workbench github pr create <ID> [--base <branch>] [--draft] [--fill]
Creates a GitHub PR using the configured provider:
- PR title: <ID>: <title>
- PR body: summary + acceptance criteria + related links
- On success: add PR URL to related.prs in front matter

#### workbench item move <ID> --to <path>
Moves a work item file and updates inbound links to the old path where possible.

#### workbench item rename <ID> --title "<...>"
Renames the work item file by regenerating the slug and updates inbound links.

#### workbench item normalize [--include-done] [--dry-run]
Normalizes work item front matter lists (tags and related lists).

#### workbench validate [--strict] [--link-include <path...>] [--link-exclude <path...>] [--skip-doc-schema]
Validates:
- front matter schema correctness
- ID/filename consistency
- duplicates across items/done
- referenced local files exist (specs/adrs)
- local markdown relative links across the repo are not broken
- branch naming convention (optional warning)
  Exit code reflects errors/warnings.

Aliases:
- `workbench verify` -> `workbench validate`

---

## Git integration rules
Implementation uses the git CLI.

Required functions:
- detect repo root (git rev-parse --show-toplevel)
- check cleanliness (git status --porcelain)
- create branch (git checkout -b ...)
- commit (git add, git commit -m ...)
- push (git push -u origin ...)

Failure behavior:
- clear error message
- structured JSON error when --format json

---

## GitHub integration rules
Default provider: Octokit.

Required actions:
- verify token presence (Octokit) or gh auth (gh provider)
- create PR and capture output URL

Octokit auth:
- use WORKBENCH_GITHUB_TOKEN (preferred) or GITHUB_TOKEN

No interactive auth flows inside Workbench.

Fallback option: gh CLI provider.

---

## Validation rules (v0.1)

Link resolution rules:
- Absolute repo paths start with `/` and are resolved from repo root.
- Relative paths are resolved from the work item file location.
- External URLs (http/https) are ignored by link validation.
- Markdown links to local resources are validated across all markdown files in the repo.

Backlink rules for related.files:
- Each target file must contain a backlink to the work item ID.
- A backlink is either a plain text mention of the ID or a markdown link to the work item file.
Examples:
- Plain text: `TASK-0042`
- Markdown link: `[TASK-0001](/docs/70-work/items/TASK-0001-improve-cli-onboarding-help-init-walkthrough-and-run-wizard.md)`

Doc cross-link rules:
- ADRs in `/docs/40-decisions` must include a "Related specs" section with at least one link to a spec in `/docs/10-product` or `/docs/20-architecture`.
- Specs referenced in `related.specs` must include a backlink to the work item ID (plain text or markdown link).

### Errors (must fail)
- Config file does not conform to workbench-config.schema.json (WB010)
- Missing required front matter fields (per work-item.schema.json)
- Invalid enum values (type/status/priority)
- Work item ID does not match file name prefix
- Duplicate IDs
- related.specs/related.adrs references to missing files
- related.files references to missing files
- related.files targets missing a backlink to the work item ID
- ADRs missing a related spec link in their "Related specs" section
- Specs referenced in related.specs missing a backlink to the work item ID
- Work item file not under configured items/done directories
- Broken local markdown links in any repo markdown file

### Warnings (may fail with --strict)
- Missing Acceptance Criteria section
- Missing spec/ADR links for ready items (team preference)
- Branch missing for in-progress item (if git available)

---

## Output format

### Table (default)
- human readable tables for list/validate
- minimal color usage; disabled with --no-color

### JSON
Stable schema per command, documented in docs/commands.md.

Error envelope:
```json
{
  "ok": false,
  "error": { "code": "WBxxx", "message": "...", "details": { } }
}
```

Success envelope:
```json
{
  "ok": true,
  "data": { }
}
```

When warnings are present, commands that support warnings include:
```json
{
  "ok": true,
  "data": { },
  "warnings": [ { "code": "WBWxx", "message": "..." } ]
}
```

---

## Exit codes
- 0: success, no warnings
- 1: success with warnings (only for validate and doctor)
- 2: command failed due to errors

---

## Error code conventions
Prefix: WB
- WB001: Not a git repo / repo not found
- WB002: git not installed / not callable
- WB010: Config parse error
- WB020: Work item not found by ID
- WB030: Front matter schema invalid
- WB040: Validation error (broken link, duplicate ID, etc.)
- WB050: Git command failed
- WB060: GitHub provider failed

---

## Security considerations
- Workbench does not store secrets.
- No credentials are collected; GitHub auth handled via environment tokens or gh.
- Octokit tokens are read from WORKBENCH_GITHUB_TOKEN (preferred) or GITHUB_TOKEN.
- Workbench should avoid printing token-related environment variables.
- When executing external tools, arguments must be safely escaped.

---

## Testing strategy
- Unit tests for:
  - slugify
  - ID allocation
  - front matter parse/write (body preserved)
  - PR body generation
  - validation rules

- Integration tests (skippable if tools missing):
  - scaffold + promote in temp git repo
  - gh tests are optional and off by default

Test data:
- /testdata/ small fixtures for markdown parsing and validation.

How to run:
- `dotnet test tests/Workbench.Tests/Workbench.Tests.csproj`
- `dotnet test Workbench.slnx` (solution-wide)

---

## Packaging
- Distributed as a .NET global tool:
  - package id: Incursa.Workbench
  - command name: workbench
- CI:
  - build/test on PR
  - pack on tag
  - publish step optional (manual/controlled)

---

## Roadmap

### v0.1 (first usable)
- scaffold
- work item create/list/show/status/close
- promote (branch + commit)
- validate
- board regen
- PR create via GitHub provider with backlink

### v0.2
- richer query/filter syntax for listing
- optional move-to-done automation on merge (via CI hook)
- hooks installer (pre-commit / pre-push validate)

### v0.3
- optional Octokit GitHub API mode
- spec assist: generate stub spec files and link them
