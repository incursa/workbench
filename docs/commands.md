# Workman CLI Commands Reference

## Global Flags

These flags work with all commands:

- `--repo <path>` - Target repository path (default: current directory)
- `--format <format>` - Output format: `table`, `json`, `yaml` (default: `table`)
- `--no-color` - Disable colored output
- `--verbose` - Enable verbose logging
- `--help` - Show help for command

## Commands

### `workman --help`

Show help for all commands.

```bash
workman --help
workman <command> --help
```

### `workman version`

Print version information.

```bash
workman version
```

**Output:**
```
Workman CLI v0.1.0
.NET Runtime: 8.0.x
Git: 2.43.0
```

**Flags:**
- `--format json` - Output version info as JSON

### `workman doctor`

Check environment and configuration health.

```bash
workman doctor
```

**Checks:**
- Git is installed and accessible
- Repository is valid (has `.git`)
- Required directories exist
- Configuration file is valid
- (Optional) GitHub CLI (`gh`) is available

**Output:**
```
✓ Git is installed (version 2.43.0)
✓ Repository is valid (/path/to/repo)
✓ Work items directory exists (/work/items)
✓ Docs directory exists (/docs)
✓ Configuration is valid
⚠ GitHub CLI (gh) not found - GitHub integration disabled

Environment: OK
```

**Exit codes:**
- `0` - All checks passed
- `1` - One or more checks failed
- `2` - Critical failure (e.g., not in a Git repo)

### `workman init`

Initialize Workman in a repository.

```bash
workman init
```

**Actions:**
1. Creates `.workman.yml` configuration file
2. Creates default directory structure (`/work/items`, `/work/done`, `/docs`)
3. Copies default templates to `/templates`
4. Creates initial `.gitignore` entries (if needed)

**Flags:**
- `--force` - Overwrite existing configuration
- `--skip-templates` - Don't copy templates

**Example:**
```bash
cd my-project
workman init
```

### `workman new <type>`

Create a new work item.

```bash
workman new bug
workman new task
workman new spike
```

**Arguments:**
- `<type>` - Type of work item: `bug`, `task`, `spike`

**Flags:**
- `--title <title>` - Work item title (required if not interactive)
- `--assignee <user>` - Assign to user
- `--priority <priority>` - Set priority: `critical`, `high`, `medium`, `low`
- `--tags <tags>` - Comma-separated tags
- `--no-edit` - Don't open editor after creation
- `--template <path>` - Use custom template

**Interactive mode:**
If `--title` is not provided, enters interactive mode prompting for:
- Title
- Priority
- Assignee
- Tags

**Output:**
```
Created BUG-0042: Fix null pointer in user service
  File: /work/items/BUG-0042-fix-null-pointer-in-user-service.md
  Status: draft
  
Opening in editor...
```

**Example:**
```bash
workman new task --title "Add pagination to API" --priority high --tags api,backend
```

### `workman list [type]`

List work items.

```bash
workman list           # All work items
workman list task      # Only tasks
workman list bug       # Only bugs
workman list spike     # Only spikes
```

**Flags:**
- `--status <status>` - Filter by status: `draft`, `ready`, `in-progress`, `review`, `done`, `cancelled`
- `--assignee <user>` - Filter by assignee
- `--tag <tag>` - Filter by tag (can be repeated)
- `--priority <priority>` - Filter by priority
- `--include-done` - Include completed items from `/work/done`
- `--sort <field>` - Sort by: `id`, `created`, `updated`, `priority`, `title` (default: `id`)
- `--reverse` - Reverse sort order

**Output (table format):**
```
ID          STATUS        PRIORITY  ASSIGNEE  TITLE
BUG-0042    in-progress   high      @alice    Fix null pointer in user service
TASK-0123   ready         medium    @bob      Add pagination to API
SPIKE-0007  in-progress   high      @charlie  Evaluate real-time options
```

**Output (json format):**
```json
[
  {
    "id": "BUG-0042",
    "title": "Fix null pointer in user service",
    "type": "bug",
    "status": "in-progress",
    "priority": "high",
    "assignee": "@alice",
    "created": "2025-01-15T09:00:00Z",
    "updated": "2025-01-15T10:30:00Z"
  }
]
```

### `workman show <id>`

Show details of a work item.

```bash
workman show BUG-0042
```

**Arguments:**
- `<id>` - Work item ID

**Output:**
```
BUG-0042: Fix null pointer in user service
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Status:      in-progress
Type:        bug
Priority:    high
Severity:    major
Assignee:    @alice
Created:     2025-01-15 09:00:00
Updated:     2025-01-15 10:30:00
Tags:        user-management, crash
Environment: production
File:        /work/items/BUG-0042-fix-null-pointer.md

Description:
When attempting to delete a user account that has no associated profile...
```

**Flags:**
- `--full` - Show full Markdown content
- `--format json` - Output as JSON

### `workman update <id>`

Update a work item.

```bash
workman update BUG-0042 --status in-progress
```

**Arguments:**
- `<id>` - Work item ID

**Flags:**
- `--status <status>` - Update status
- `--assignee <user>` - Update assignee
- `--priority <priority>` - Update priority
- `--add-tag <tag>` - Add a tag (can be repeated)
- `--remove-tag <tag>` - Remove a tag (can be repeated)
- `--branch <branch>` - Set associated branch
- `--pr <number>` - Set associated PR number

**Actions:**
- Updates the front matter in the work item file
- Updates the `updated` timestamp
- If status is changed to `done`, optionally moves to `/work/done`

**Example:**
```bash
workman update TASK-0123 --status review --pr 456
```

### `workman validate [path]`

Validate work items.

```bash
workman validate                    # Validate all work items
workman validate work/items/*.md    # Validate specific files
```

**Checks:**
- Front matter syntax is valid YAML
- Required fields are present
- Field values are valid (enums, dates, etc.)
- IDs are unique
- File naming matches ID

**Output:**
```
Validating 15 work items...

✓ BUG-0042: Valid
✓ TASK-0123: Valid
✗ SPIKE-0007: Missing required field 'created'
✗ TASK-0099: Invalid status 'in_progress' (should be 'in-progress')

Results: 13 valid, 2 invalid
```

**Exit codes:**
- `0` - All valid
- `1` - One or more invalid

### `workman search <query>`

Search work items by text.

```bash
workman search "authentication"
workman search "null pointer" --type bug
```

**Arguments:**
- `<query>` - Search query (searches title and content)

**Flags:**
- `--type <type>` - Filter by type
- `--status <status>` - Filter by status
- `--case-sensitive` - Case-sensitive search
- `--regex` - Treat query as regex

**Output:**
```
Found 3 work items matching "authentication":

TASK-0050: Implement JWT authentication
  /work/items/TASK-0050-jwt-auth.md
  ... JWT-based authentication to the API...

BUG-0033: Auth token expiration issue
  /work/items/BUG-0033-token-expiry.md
  ... authentication tokens are expiring prematurely...
```

### `workman branch <id>`

Create a Git branch for a work item.

```bash
workman branch TASK-0123
```

**Arguments:**
- `<id>` - Work item ID

**Flags:**
- `--base <branch>` - Base branch (default: current branch)
- `--checkout` - Checkout branch after creation (default: true)
- `--push` - Push branch to remote

**Branch naming:**
- Bugs: `bug/BUG-0042-short-title`
- Tasks: `feature/TASK-0123-short-title`
- Spikes: `spike/SPIKE-0007-short-title`

**Example:**
```bash
workman branch TASK-0123
# Creates and checks out: feature/TASK-0123-add-pagination
```

### `workman stats`

Show repository statistics.

```bash
workman stats
```

**Output:**
```
Work Item Statistics
━━━━━━━━━━━━━━━━━━━━
Total Items:     47
  Tasks:         28
  Bugs:          15
  Spikes:        4

By Status:
  Draft:         5
  Ready:         8
  In Progress:   12
  Review:        6
  Done:          16

By Priority:
  Critical:      2
  High:          8
  Medium:        22
  Low:           15

By Assignee:
  @alice:        12
  @bob:          8
  @charlie:      5
  Unassigned:    22
```

**Flags:**
- `--format json` - Output as JSON
- `--include-done` - Include completed items

### `workman config`

Manage Workman configuration.

```bash
workman config show                    # Show current config
workman config get paths.items         # Get specific value
workman config set paths.items work    # Set specific value
```

**Subcommands:**
- `show` - Display full configuration
- `get <key>` - Get configuration value
- `set <key> <value>` - Set configuration value

## Future Commands (Roadmap)

These commands are planned but not yet implemented:

- `workman import` - Import from GitHub Issues, Jira, etc.
- `workman export` - Export to various formats
- `workman report` - Generate reports (burndown, velocity, etc.)
- `workman link` - Link to GitHub Issues/PRs
- `workman template` - Manage custom templates
- `workman migrate` - Migrate work items between repos

## Exit Codes

All commands use standard exit codes:

- `0` - Success
- `1` - General error
- `2` - Invalid usage (bad arguments, flags)
- `3` - Not in a Git repository
- `4` - Configuration error

## Environment Variables

- `WORKMAN_REPO` - Default repository path
- `WORKMAN_FORMAT` - Default output format
- `WORKMAN_NO_COLOR` - Disable colors (any value)
- `WORKMAN_EDITOR` - Editor for interactive editing (falls back to `EDITOR`)

## Examples

### Typical Workflow

```bash
# Initialize in your project
cd my-project
workman init

# Create a new task
workman new task --title "Add user search" --priority high

# List current work
workman list --status in-progress

# Update a work item
workman update TASK-0150 --status review --pr 789

# Create branch for work item
workman branch TASK-0151

# Check environment
workman doctor

# Validate all work items
workman validate
```

### Automation Examples

```bash
# Get all high-priority bugs as JSON
workman list bug --priority high --format json

# Count items by status
workman list --status in-progress --format json | jq 'length'

# Find all items assigned to a user
workman list --assignee alice --format json
```
