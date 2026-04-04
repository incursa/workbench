# Workbench CLI Help

Generated from the live `System.CommandLine` tree.
Regenerate with `workbench doc regen-help`.
Verify drift with `workbench doc regen-help --check`.

The generated snapshot lives at `specs/generated/commands.md`.

## Usage
```text
workbench <command> [options]
```

## Global options
- `-?`, `-h`, `/?`, `/h`: Show help and usage information
- `--version`: Show version information
- `--repo <repo>`: Target repo (defaults to current dir)
- `--format <format>`: Output format (table|json)
- `--no-color`: Disable colored output
- `--quiet`: Suppress non-error output
- `--debug`: Print full exception diagnostics on failure.

## Sync model
- `workbench sync`: umbrella command that runs the item, doc, and nav sync stages. Use this for the common happy path.
- `workbench item sync`: external sync stage for GitHub issues, imports, and branch state.
- `workbench doc sync`: repo metadata stage for doc/work-item backlinks and doc front matter.
- `workbench spec`: dedicated requirement-spec workflow for creation, inspection, editing, linking, unlinking, deletion, and sync.
- `workbench nav sync`: derived view stage for canonical indexes and backlink maintenance.

## Config
- Repo config path: `.workbench/config.json`.

## Exit codes
- `0`: success, no warnings.
- `1`: success with warnings (`doctor` and `validate`).
- `2`: command failed due to errors.

## Command tree
- `workbench`
  - `workbench codex`: Group: Codex agent commands.
    - `workbench codex doctor`: Check whether Codex is installed and callable.
    - `workbench codex run`: Run Codex in full-auto mode with web search.
  - `workbench config`: Group: configuration commands.
    - `workbench config credentials`: Manage credentials.env entries.
      - `workbench config credentials set`: Set an entry in credentials.env.
      - `workbench config credentials unset`: Remove an entry from credentials.env.
    - `workbench config set`: Write or update config values in .workbench/config.json.
    - `workbench config show`: Print effective config (defaults + repo config + CLI overrides).
  - `workbench doc`: Group: documentation commands.
    - `workbench doc delete`: Delete a documentation file and update work item links.
    - `workbench doc edit`: Edit documentation metadata and body by artifact ID or path.
    - `workbench doc link`: Link a doc to work items.
    - `workbench doc new`: Create a documentation file with Workbench front matter.
    - `workbench doc regen-help`: Regenerate specs/generated/commands.md from the live command tree.
    - `workbench doc show`: Show a documentation file by artifact ID or path.
    - `workbench doc summarize`: Summarize doc changes with AI and append change notes.
    - `workbench doc sync`: Repo metadata stage: sync doc/work-item backlinks and doc front matter. Does not regenerate indexes.
    - `workbench doc unlink`: Unlink a doc from work items.
  - `workbench doctor`: Check git, config, and expected paths.
  - `workbench github`: Group: GitHub commands.
    - `workbench github pr`: Group: GitHub pull request commands.
      - `workbench github pr create`: Create a GitHub PR via the configured provider and backlink the PR URL.
  - `workbench guide`: Run the interactive guide for common tasks.
  - `workbench init`: Interactive setup for Workbench (scaffold + guidance + guide).
  - `workbench item`: Group: work item commands.
    - `workbench item close`: Set status to complete.
    - `workbench item delete`: Delete a work item file and update doc backlinks.
    - `workbench item edit`: Safely edit structured work item fields and keep title/body/slug alignment coherent.
    - `workbench item generate`: Generate a work item draft with AI and create it.
    - `workbench item import`: Import GitHub issues into work items.
    - `workbench item link`: Link specs, files, PRs, or issues to a work item.
    - `workbench item list`: List work items.
    - `workbench item move`: Move a work item file and update inbound links where possible.
    - `workbench item new`: Create a new work item in specs/work-items using templates and ID allocation.
    - `workbench item normalize`: Normalize work item front matter lists.
    - `workbench item rename`: Regenerate slug from title, rename the file, and update inbound links.
    - `workbench item show`: Show metadata and resolved path for an item.
    - `workbench item status`: Update status and updated date.
    - `workbench item sync`: External sync stage: reconcile local work items with GitHub issues and branch state.
    - `workbench item unlink`: Remove specs, files, PRs, or issues from a work item.
  - `workbench llm`: Group: AI-oriented help and guidance commands.
    - `workbench llm help`: Print a comprehensive CLI reference for AI agents.
  - `workbench nav`: Group: navigation/index commands.
    - `workbench nav sync`: Derived view stage: regenerate canonical indexes, syncing links first by default.
  - `workbench normalize`: Normalize work item and doc front matter.
  - `workbench promote`: Create a work item, branch, and commit in one step.
  - `workbench quality`: Group: repo-native quality evidence commands.
    - `workbench quality attest`: Generate a derived repository evidence snapshot as HTML and JSON.
    - `workbench quality show`: Read the latest normalized quality artifact or a selected evidence kind.
    - `workbench quality sync`: Discover testing evidence, ingest normalized artifacts, and generate the current quality report.
  - `workbench scaffold`: Create the default folder structure, templates, and config.
  - `workbench spec`: Group: specification commands.
    - `workbench spec delete`: Delete a specification file and update work item links.
    - `workbench spec edit`: Edit specification metadata and body by artifact ID or path.
    - `workbench spec link`: Link a specification to work items.
    - `workbench spec new`: Create a specification file with canonical front matter.
    - `workbench spec show`: Show a specification by artifact ID or path.
    - `workbench spec sync`: Repo metadata stage: sync spec/work-item backlinks and spec front matter. Does not regenerate indexes.
    - `workbench spec unlink`: Unlink a specification from work items.
  - `workbench sync`: Umbrella repo sync: run the item, doc, and nav sync stages. Use this for the common happy path.
  - `workbench validate`: Validate repository artifacts, links, and trace profiles.
  - `workbench version`: Print CLI version.
  - `workbench voice`: Group: voice input commands.
    - `workbench voice doc`: Create a documentation file from voice input.
    - `workbench voice workitem`: Create a work item from voice input.
  - `workbench worktree`: Group: git worktree commands.
    - `workbench worktree start`: Create or reuse a task worktree.

## Detailed command reference

### `workbench codex`
Group: Codex agent commands.

Subcommands:
- `doctor`: Check whether Codex is installed and callable.
- `run`: Run Codex in full-auto mode with web search.

### `workbench codex doctor`
Check whether Codex is installed and callable.

### `workbench codex run`
Run Codex in full-auto mode with web search.

Options:
- `--prompt <prompt>` (required): Prompt to send to Codex.
- `--terminal`: Launch in a separate terminal window instead of waiting for output.

### `workbench config`
Group: configuration commands.

Subcommands:
- `credentials`: Manage credentials.env entries.
- `set`: Write or update config values in .workbench/config.json.
- `show`: Print effective config (defaults + repo config + CLI overrides).

### `workbench config credentials`
Manage credentials.env entries.

Subcommands:
- `set`: Set an entry in credentials.env.
- `unset`: Remove an entry from credentials.env.

### `workbench config credentials set`
Set an entry in credentials.env.

Options:
- `--key <key>` (required): Environment variable name.
- `--value <value>` (required): Environment variable value.
- `--path <path>`: Credentials file path (defaults to .workbench/credentials.env).

### `workbench config credentials unset`
Remove an entry from credentials.env.

Options:
- `--key <key>` (required): Environment variable name.
- `--path <path>`: Credentials file path (defaults to .workbench/credentials.env).

### `workbench config set`
Write or update config values in .workbench/config.json.

Options:
- `--path <path>` (required): Config path in dot notation (e.g., paths.docsRoot).
- `--value <value>` (required): Config value (string by default).
- `--json`: Parse the value as JSON (for booleans, numbers, or objects).

### `workbench config show`
Print effective config (defaults + repo config + CLI overrides).

### `workbench doc`
Group: documentation commands.

Subcommands:
- `delete`: Delete a documentation file and update work item links.
- `edit`: Edit documentation metadata and body by artifact ID or path.
- `link`: Link a doc to work items.
- `new`: Create a documentation file with Workbench front matter.
- `regen-help`: Regenerate specs/generated/commands.md from the live command tree.
- `show`: Show a documentation file by artifact ID or path.
- `summarize`: Summarize doc changes with AI and append change notes.
- `sync`: Repo metadata stage: sync doc/work-item backlinks and doc front matter. Does not regenerate indexes.
- `unlink`: Unlink a doc from work items.

### `workbench doc delete`
Delete a documentation file and update work item links.

Options:
- `--path <path>` (required): Doc path, link, or artifact ID.
- `--keep-links`: Skip removing doc links from work items.

### `workbench doc edit`
Edit documentation metadata and body by artifact ID or path.

Arguments:
- `reference`: Artifact ID or doc path.

Options:
- `--artifact-id <artifact-id>`: Update the artifact identifier.
- `--title <title>`: Update the doc title.
- `--status <status>`: Update the doc status.
- `--owner <owner>`: Update the doc owner.
- `--domain <domain>`: Update the document domain metadata.
- `--capability <capability>`: Update the document capability metadata.
- `--body <body>`: Replace the Markdown body with the provided text.
- `--body-file <body-file>`: Replace the Markdown body with file contents.
- `--work-item <work-item>`: Replace the linked work item list.
- `--code-ref <code-ref>`: Replace the linked code ref list.

### `workbench doc link`
Link a doc to work items.

Options:
- `--type <type>` (required): Doc type: specification, architecture, verification, work_item, doc
- `--path <path>` (required): Doc path, link, or artifact ID.
- `--work-item <work-item>`: Work item ID(s) to link.
- `--dry-run`: Report changes without writing files.

### `workbench doc new`
Create a documentation file with Workbench front matter.

Options:
- `--type <type>` (required): Doc type: specification, architecture, verification, work_item, doc
- `--title <title>` (required): Doc title
- `--path <path>`: Destination path (defaults by type).
- `--artifact-id <artifact-id>`: Artifact identifier for canonical specs, architecture docs, verification docs, and work items.
- `--domain <domain>`: Document domain metadata used when generating canonical artifact IDs.
- `--capability <capability>`: Document capability metadata used when generating specification IDs.
- `--work-item <work-item>`: Link one or more work items.
- `--code-ref <code-ref>`: Add code reference(s) (e.g., src/Foo.cs#L10-L20).
- `--force`: Overwrite existing file.

### `workbench doc regen-help`
Regenerate specs/generated/commands.md from the live command tree.

Options:
- `--check`: Fail if specs/generated/commands.md is out of date.
- `--path <path>`: Output path (defaults to specs/generated/commands.md).

### `workbench doc show`
Show a documentation file by artifact ID or path.

Arguments:
- `reference`: Artifact ID or doc path.

### `workbench doc summarize`
Summarize doc changes with AI and append change notes.

Options:
- `--staged`: Use staged diff (default when no --path is provided).
- `--path <path>`: File path(s) to summarize (defaults to staged markdown files).
- `--dry-run`: Report changes without writing files.
- `--update-index`: Run git add on updated files.

### `workbench doc sync`
Repo metadata stage: sync doc/work-item backlinks and doc front matter. Does not regenerate indexes.

Options:
- `--all`: Add or normalize Workbench front matter on all docs (default).
- `--issues`: Sync GitHub issue links while updating doc/work-item backlinks.
- `--include-terminal-items`: Include terminal work items.
- `--dry-run`: Report changes without writing files.

### `workbench doc unlink`
Unlink a doc from work items.

Options:
- `--type <type>` (required): Doc type: specification, architecture, verification, work_item, doc
- `--path <path>` (required): Doc path, link, or artifact ID.
- `--work-item <work-item>`: Work item ID(s) to unlink.
- `--dry-run`: Report changes without writing files.

### `workbench doctor`
Check git, config, and expected paths.

Options:
- `--json`: Output machine-readable JSON.

### `workbench github`
Group: GitHub commands.

Subcommands:
- `pr`: Group: GitHub pull request commands.

### `workbench github pr`
Group: GitHub pull request commands.

Subcommands:
- `create`: Create a GitHub PR via the configured provider and backlink the PR URL.

### `workbench github pr create`
Create a GitHub PR via the configured provider and backlink the PR URL.

Arguments:
- `id`: Work item ID.

Options:
- `--base <base>`: Base branch for PR.
- `--draft`: Create as draft.
- `--fill`: Fill PR body from work item.

### `workbench guide`
Run the interactive guide for common tasks.

### `workbench init`
Interactive setup for Workbench (scaffold + guidance + guide).

Options:
- `--force`: Overwrite existing files.
- `--non-interactive`: Run init without prompts (use flags to enable steps).
- `--skip-guide`: Skip launching the interactive guide after init.
- `--front-matter`: Add Workbench front matter to docs (non-interactive).
- `--configure-openai`: Configure OpenAI settings (non-interactive).
- `--credential-store <credential-store>`: Credential storage: local, external, skip.
- `--credential-path <credential-path>`: Credentials file path for local or external storage.
- `--openai-provider <openai-provider>`: AI provider (openai|none).
- `--openai-key <openai-key>`: OpenAI API key (stored in credentials file).
- `--openai-model <openai-model>`: OpenAI model (default: gpt-4o-mini).

### `workbench item`
Group: work item commands.

Subcommands:
- `close`: Set status to complete.
- `delete`: Delete a work item file and update doc backlinks.
- `edit`: Safely edit structured work item fields and keep title/body/slug alignment coherent.
- `generate`: Generate a work item draft with AI and create it.
- `import`: Import GitHub issues into work items.
- `link`: Link specs, files, PRs, or issues to a work item.
- `list`: List work items.
- `move`: Move a work item file and update inbound links where possible.
- `new`: Create a new work item in specs/work-items using templates and ID allocation.
- `normalize`: Normalize work item front matter lists.
- `rename`: Regenerate slug from title, rename the file, and update inbound links.
- `show`: Show metadata and resolved path for an item.
- `status`: Update status and updated date.
- `sync`: External sync stage: reconcile local work items with GitHub issues and branch state.
- `unlink`: Remove specs, files, PRs, or issues from a work item.

### `workbench item close`
Set status to complete.

Arguments:
- `id`: Work item ID.

### `workbench item delete`
Delete a work item file and update doc backlinks.

Arguments:
- `id`: Work item ID.

Options:
- `--keep-links`: Skip removing doc backlinks.

### `workbench item edit`
Safely edit structured work item fields and keep title/body/slug alignment coherent.

Arguments:
- `id`: Work item ID.

Options:
- `--title <title>`: New title. Updates front matter, the H1 heading, and the file slug by default.
- `--summary <summary>`: Replace the Summary section with inline text.
- `--summary-file <summary-file>`: Replace the Summary section from a text file.
- `--acceptance <acceptance>`: Replace Acceptance criteria with one or more list entries.
- `--acceptance-file <acceptance-file>`: Replace Acceptance criteria from a text file (one item per non-empty line).
- `--append-note <append-note>`: Append a bullet to the Notes section.
- `--keep-path`: Do not rename the file slug when updating the title.

### `workbench item generate`
Generate a work item draft with AI and create it.

Options:
- `--prompt <prompt>` (required): Freeform description for the AI-generated work item.
- `--type <type>`: Work item type: work_item (defaults to AI choice).
- `--status <status>`: Work item status: planned, in_progress, blocked, complete, cancelled, superseded
- `--priority <priority>`: Work item priority
- `--owner <owner>`: Work item owner

### `workbench item import`
Import GitHub issues into work items.

Options:
- `--issue <issue>` (required): Issue numbers or URLs to import.
- `--type <type>`: Work item type: work_item (defaults based on labels).
- `--status <status>`: Work item status: planned, in_progress, blocked, complete, cancelled, superseded
- `--priority <priority>`: Work item priority
- `--owner <owner>`: Work item owner

### `workbench item link`
Link specs, files, PRs, or issues to a work item.

Arguments:
- `id`: Work item ID.

Options:
- `--spec <spec>`: Spec path(s) to link.
- `--file <file>`: File path(s) to link.
- `--pr <pr>`: PR URL(s) to link.
- `--issue <issue>`: Issue URL(s) or IDs to link.
- `--dry-run`: Report changes without writing files.

### `workbench item list`
List work items.

Options:
- `--type <type>`: Filter by type: work_item
- `--status <status>`: Filter by status: planned, in_progress, blocked, complete, cancelled, superseded
- `--include-terminal-items`: Include terminal items.

### `workbench item move`
Move a work item file and update inbound links where possible.

Arguments:
- `id`: Work item ID.

Options:
- `--to <to>` (required): Destination path.

### `workbench item new`
Create a new work item in specs/work-items using templates and ID allocation.

Options:
- `--type <type>` (required): Work item type: work_item
- `--title <title>` (required): Work item title
- `--status <status>`: Work item status: planned, in_progress, blocked, complete, cancelled, superseded
- `--priority <priority>`: Work item priority
- `--owner <owner>`: Work item owner

### `workbench item normalize`
Normalize work item front matter lists.

Options:
- `--include-terminal-items`: Include terminal items.
- `--dry-run`: Report changes without writing files.

### `workbench item rename`
Regenerate slug from title, rename the file, and update inbound links.

Arguments:
- `id`: Work item ID.

Options:
- `--title <title>` (required): New title.

### `workbench item show`
Show metadata and resolved path for an item.

Arguments:
- `id`: Work item ID (e.g., TASK-0042).

### `workbench item status`
Update status and updated date.

Arguments:
- `id`: Work item ID.
- `status`: New status: planned, in_progress, blocked, complete, cancelled, superseded.

Options:
- `--note <note>`: Append a note.

### `workbench item sync`
External sync stage: reconcile local work items with GitHub issues and branch state.

Options:
- `--id <id>`: Limit external sync to specific work item IDs.
- `--issue <issue>`: Import or reconcile specific GitHub issues by number or URL.
- `--prefer <prefer>`: When local and GitHub descriptions differ, prefer 'local' or 'github'.
- `--dry-run`: Report changes without writing.
- `--import-issues`: Import unlinked GitHub issues into local work items (slower).

### `workbench item unlink`
Remove specs, files, PRs, or issues from a work item.

Arguments:
- `id`: Work item ID.

Options:
- `--spec <spec>`: Spec path(s) to unlink.
- `--file <file>`: File path(s) to unlink.
- `--pr <pr>`: PR URL(s) to unlink.
- `--issue <issue>`: Issue URL(s) or IDs to unlink.
- `--dry-run`: Report changes without writing files.

### `workbench llm`
Group: AI-oriented help and guidance commands.

Aliases: `llms`

Subcommands:
- `help`: Print a comprehensive CLI reference for AI agents.

### `workbench llm help`
Print a comprehensive CLI reference for AI agents.

### `workbench nav`
Group: navigation/index commands.

Subcommands:
- `sync`: Derived view stage: regenerate canonical indexes, syncing links first by default.

### `workbench nav sync`
Derived view stage: regenerate canonical indexes, syncing links first by default.

Options:
- `--issues`: Sync GitHub issue links while rebuilding derived navigation views.
- `--force`: Rewrite derived index sections even if content is unchanged.
- `--include-terminal-items`: Include terminal-status work items in derived indexes.
- `--dry-run`: Report changes without writing files.

### `workbench normalize`
Normalize work item and doc front matter.

Options:
- `--items`: Normalize work item front matter.
- `--docs`: Normalize doc front matter.
- `--include-terminal-items`: Include terminal items.
- `--all-docs`: Add Workbench front matter to all docs (default).
- `--dry-run`: Report changes without writing files.

### `workbench promote`
Create a work item, branch, and commit in one step.

Options:
- `--type <type>` (required): Work item type: work_item
- `--title <title>` (required): Work item title
- `--push`: Push the branch to origin.
- `--start`: Set status to in-progress.
- `--pr`: Create a GitHub PR.
- `--base <base>`: Base branch for PR.
- `--draft`: Create a draft PR.
- `--no-draft`: Create a ready PR.

### `workbench quality`
Group: repo-native quality evidence commands.

Subcommands:
- `attest`: Generate a derived repository evidence snapshot as HTML and JSON.
- `show`: Read the latest normalized quality artifact or a selected evidence kind.
- `sync`: Discover testing evidence, ingest normalized artifacts, and generate the current quality report.

### `workbench quality attest`
Generate a derived repository evidence snapshot as HTML and JSON.

Options:
- `--scope <scope>`: Repo-relative path prefixes or files to include in the snapshot scope.
- `--profile <profile>`: Validation profile to use for the snapshot (core|traceable|auditable).
- `--emit <emit>`: Derived output format to write (html|json|both).
- `--out-dir <out-dir>`: Directory for derived attestation artifacts.
- `--config <config>`: Optional attestation config path.
- `--results <results>`: TRX file or directory root to ingest for evidence.
- `--coverage <coverage>`: Cobertura file or directory root to ingest for evidence.
- `--benchmarks <benchmarks>`: Benchmark evidence file or directory root to inspect.
- `--manual-qa <manual-qa>`: Manual QA evidence file or directory root to inspect.
- `--exec`: Run configured evidence refresh commands before generating the snapshot.
- `--no-exec`: Do not execute configured evidence refresh commands.

### `workbench quality show`
Read the latest normalized quality artifact or a selected evidence kind.

Options:
- `--kind <kind>`: Artifact kind to show (report|inventory|results|coverage).
- `--path <path>`: Optional explicit artifact path to read.

### `workbench quality sync`
Discover testing evidence, ingest normalized artifacts, and generate the current quality report.

Options:
- `--contract <contract>`: Authored testing intent contract path.
- `--results <results>`: TRX file or directory root to ingest.
- `--coverage <coverage>`: Cobertura file or directory root to ingest.
- `--out-dir <out-dir>`: Directory for normalized quality artifacts.
- `--dry-run`: Compute the quality artifacts without writing files.
- `--sync-requirement-comments`: Synchronize generated XML-style requirement comment blocks into test source files.

### `workbench scaffold`
Create the default folder structure, templates, and config.

Options:
- `--force`: Overwrite existing files.

### `workbench spec`
Group: specification commands.

Subcommands:
- `delete`: Delete a specification file and update work item links.
- `edit`: Edit specification metadata and body by artifact ID or path.
- `link`: Link a specification to work items.
- `new`: Create a specification file with canonical front matter.
- `show`: Show a specification by artifact ID or path.
- `sync`: Repo metadata stage: sync spec/work-item backlinks and spec front matter. Does not regenerate indexes.
- `unlink`: Unlink a specification from work items.

### `workbench spec delete`
Delete a specification file and update work item links.

Options:
- `--path <path>` (required): Spec path, link, or artifact ID.
- `--keep-links`: Skip removing spec links from work items.

### `workbench spec edit`
Edit specification metadata and body by artifact ID or path.

Arguments:
- `reference`: Artifact ID or spec path.

Options:
- `--artifact-id <artifact-id>`: Update the artifact identifier.
- `--title <title>`: Update the spec title.
- `--status <status>`: Update the spec status.
- `--owner <owner>`: Update the spec owner.
- `--domain <domain>`: Update the spec domain metadata.
- `--capability <capability>`: Update the spec capability metadata.
- `--body <body>`: Replace the Markdown body with the provided text.
- `--body-file <body-file>`: Replace the Markdown body with file contents.
- `--work-item <work-item>`: Replace the linked work item list.
- `--code-ref <code-ref>`: Replace the linked code ref list.

### `workbench spec link`
Link a specification to work items.

Options:
- `--path <path>` (required): Spec path, link, or artifact ID.
- `--work-item <work-item>`: Work item ID(s) to link.
- `--dry-run`: Report changes without writing files.

### `workbench spec new`
Create a specification file with canonical front matter.

Options:
- `--title <title>` (required): Spec title
- `--path <path>`: Destination path (defaults under specs/).
- `--artifact-id <artifact-id>`: Artifact identifier for the spec.
- `--domain <domain>`: Spec domain metadata used when generating IDs.
- `--capability <capability>`: Spec capability metadata used when generating IDs.
- `--work-item <work-item>`: Link one or more work items.
- `--code-ref <code-ref>`: Add code reference(s) (e.g., src/Foo.cs#L10-L20).
- `--force`: Overwrite existing file.

### `workbench spec show`
Show a specification by artifact ID or path.

Arguments:
- `reference`: Artifact ID or spec path.

### `workbench spec sync`
Repo metadata stage: sync spec/work-item backlinks and spec front matter. Does not regenerate indexes.

Options:
- `--all`: Add or normalize Workbench front matter on all specs (default).
- `--issues`: Sync GitHub issue links while updating spec/work-item backlinks.
- `--include-terminal-items`: Include terminal work items.
- `--dry-run`: Report changes without writing files.

### `workbench spec unlink`
Unlink a specification from work items.

Options:
- `--path <path>` (required): Spec path, link, or artifact ID.
- `--work-item <work-item>`: Work item ID(s) to unlink.
- `--dry-run`: Report changes without writing files.

### `workbench sync`
Umbrella repo sync: run the item, doc, and nav sync stages. Use this for the common happy path.

Options:
- `--items`: Run the `item sync` stage (GitHub issues/branches <-> local work items).
- `--docs`: Run the `doc sync` stage (backlinks + doc front matter).
- `--nav`: Run the `nav sync` stage (derived indexes).
- `--issues`: Sync GitHub issue links in the doc and nav stages.
- `--import-issues`: Pass through to the item sync stage to import unlinked GitHub issues (slower).
- `--include-terminal-items`: Include terminal work items in the doc and nav stages.
- `--force`: Pass through to the nav sync stage to rewrite derived sections even if unchanged.
- `--dry-run`: Report changes without writing files.
- `--prefer <prefer>`: Pass through to the item sync stage when local and GitHub descriptions differ.

### `workbench validate`
Validate repository artifacts, links, and trace profiles.

Aliases: `verify`

Options:
- `--strict`: Treat warnings as errors.
- `--verbose`: Show detailed validation output.
- `--link-include <link-include>`: Repo-relative path prefixes to include in link validation.
- `--link-exclude <link-exclude>`: Repo-relative path prefixes to exclude from link validation.
- `--skip-doc-schema`: Skip doc front matter schema validation.
- `--profile <profile>`: Validation profile to enforce (core|traceable|auditable).
- `--scope <scope>`: Repo-relative path prefixes or files to validate.

### `workbench version`
Print CLI version.

### `workbench voice`
Group: voice input commands.

Subcommands:
- `doc`: Create a documentation file from voice input.
- `workitem`: Create a work item from voice input.

### `workbench voice doc`
Create a documentation file from voice input.

Options:
- `--type <type>` (required): Doc type: specification, architecture, verification, work_item, doc
- `--out <out>`: Output path (defaults by type).
- `--title <title>`: Doc title (optional).

### `workbench voice workitem`
Create a work item from voice input.

Options:
- `--type <type>`: Work item type: work_item (defaults to AI choice).
- `--status <status>`: Work item status: planned, in_progress, blocked, complete, cancelled, superseded
- `--priority <priority>`: Work item priority
- `--owner <owner>`: Work item owner

### `workbench worktree`
Group: git worktree commands.

Subcommands:
- `start`: Create or reuse a task worktree.

### `workbench worktree start`
Create or reuse a task worktree.

Options:
- `--slug <slug>` (required): Short task slug used for branch and directory naming.
- `--ticket <ticket>`: Optional numeric ticket to prefix the branch.
- `--base <base>`: Base branch for new branches (defaults to config git.defaultBaseBranch).
- `--root <root>`: Root directory for worktrees (defaults to <repo>.worktrees).
- `--prompt <prompt>`: Prompt to send when launching Codex.
- `--start-codex`: Launch Codex after creating/reusing the worktree.
- `--codex-terminal`: When launching Codex, use a separate terminal window.
