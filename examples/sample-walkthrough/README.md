# Sample Workbench Walkthrough

This directory contains a step-by-step walkthrough that demonstrates Workbench
in action with real examples. Follow along to see the complete workflow from
initialization to validation.

## What You'll Learn

- How to initialize a Workbench repository
- Creating and managing work items
- Writing structured documentation
- Linking docs and work items
- Validating your repository
- GitHub integration basics

## Prerequisites

- Workbench installed (see [Getting Started](../../docs/00-overview/getting-started.md))
- A test git repository (or use the steps below to create one)
- Optional: GitHub CLI (`gh`) for GitHub integration features

## Walkthrough Steps

### Step 1: Create a test repository

```bash
# Create a new directory for testing
mkdir workbench-demo
cd workbench-demo

# Initialize git
git init
git config user.name "Your Name"
git config user.email "your.email@example.com"

# Create initial commit
echo "# Workbench Demo" > README.md
git add README.md
git commit -m "Initial commit"
```

### Step 2: Initialize Workbench

Run the interactive initialization:

```bash
workbench init
```

When prompted:
- **Scaffold default structure?** Yes
- **Add front matter to existing docs?** Yes (if you have any)
- **Configure OpenAI?** Skip (or configure if you have an API key)
- **Credential storage location?** Local file (for demo purposes)
- **Launch wizard after init?** Yes (or No if you prefer to follow manually)

**What happened?**
- Created `.workbench/config.json` with default settings
- Created directory structure:
  - `docs/00-overview/` through `docs/70-work/`
  - `docs/70-work/items/` for active work items
  - `docs/70-work/done/` for completed work items
  - `docs/70-work/templates/` for work item templates
- Created template files for bug, task, and spike work items
- Added `.workbench/credentials.env` to `.gitignore` (if using local storage)

### Step 3: Verify the setup

Check that everything is configured correctly:

```bash
workbench doctor
```

You should see output indicating:
- ✓ Git repository found
- ✓ Configuration valid
- ✓ Required directories exist
- ⚠ GitHub provider (warning is OK if not configured)
- ⚠ OpenAI credentials (warning is OK if not configured)

### Step 4: Create your first work item

Create a task to add a user guide:

```bash
workbench item new --type task \
  --title "Create user guide for the API" \
  --priority high \
  --owner platform
```

**Result:** A new file is created at `docs/70-work/items/TASK-0001-create-user-guide-for-the-api.md`

View the created work item:

```bash
workbench item show TASK-0001
```

Or open it directly:

```bash
cat docs/70-work/items/TASK-0001-create-user-guide-for-the-api.md
```

### Step 5: Create another work item

Create a bug to fix:

```bash
workbench item new --type bug \
  --title "Fix authentication timeout issue" \
  --priority critical \
  --status in-progress
```

**Result:** Creates `docs/70-work/items/BUG-0001-fix-authentication-timeout-issue.md`

### Step 6: List your work items

See all open work items:

```bash
workbench item list
```

Filter by type or status:

```bash
workbench item list --type task
workbench item list --status in-progress
```

### Step 7: Create documentation

Create a feature specification:

```bash
workbench doc new --type spec \
  --title "API Authentication Flow" \
  --work-item TASK-0001
```

This creates a spec document and automatically links it to `TASK-0001`.

Create an architecture decision record:

```bash
workbench doc new --type adr \
  --title "Use JWT tokens for API authentication"
```

### Step 8: Manually edit a work item

Open `docs/70-work/items/TASK-0001-create-user-guide-for-the-api.md` and add content:

```markdown
## Summary

Create comprehensive user documentation for the REST API, including authentication,
endpoints, and code examples.

## Acceptance criteria

- [ ] Document all public API endpoints
- [ ] Include authentication setup guide
- [ ] Provide code examples in Python and JavaScript
- [ ] Add troubleshooting section
- [ ] Review with product team

## Notes

- Target audience: external developers
- Should integrate with existing docs site
```

### Step 9: Link documents and work items

Link the ADR to the bug work item:

```bash
workbench item link BUG-0001 \
  --adr docs/40-decisions/use-jwt-tokens-for-api-authentication.md
```

View the updated work item to see the link:

```bash
workbench item show BUG-0001
```

### Step 10: Update work item status

Mark the bug as ready for review:

```bash
workbench item status BUG-0001 done --note "Implemented JWT authentication with 30min timeout"
```

### Step 11: Sync navigation and indexes

Update all navigation indexes in README files:

```bash
workbench nav sync
```

**What happened?**
- Updated `docs/70-work/README.md` with a workboard table
- Updated `docs/README.md` with doc indexes
- Updated `README.md` in the repo root with work item stats

Check the changes:

```bash
cat docs/70-work/README.md
cat docs/README.md
```

### Step 12: Validate the repository

Run validation to check for issues:

```bash
workbench validate
```

This checks:
- Work item YAML front matter is valid
- Document front matter is valid
- Links between docs and work items are correct
- No duplicate IDs
- No broken internal links

Fix any issues reported, then run again:

```bash
workbench validate --strict
```

In `--strict` mode, warnings become errors (useful for CI).

### Step 13: Close a work item

Mark the bug as done and move it to the done directory:

```bash
workbench item close BUG-0001 --move
```

**Result:** The file moves from `docs/70-work/items/` to `docs/70-work/done/`

Verify by listing items with done items included:

```bash
workbench item list --include-done
```

### Step 14: Commit your work

Review what was created:

```bash
git status
```

Commit the changes:

```bash
git add .
git commit -m "Initialize Workbench and create sample work items"
```

## Advanced: GitHub Integration

If you want to try GitHub integration, you'll need:

1. A GitHub repository
2. GitHub CLI authenticated (`gh auth login`)

Then you can:

### Create a work item with a branch and PR

```bash
workbench promote --type task \
  --title "Add rate limiting to API" \
  --start --pr --draft
```

This creates:
- A work item (`TASK-0002` or next available ID)
- A git branch (`work/TASK-0002-add-rate-limiting-to-api`)
- An initial commit
- A draft GitHub pull request
- Links the PR to the work item

### Import GitHub issues as work items

```bash
workbench item import --issue 42
```

### Sync work items with GitHub issues

```bash
workbench sync --items --issues
```

This syncs status and metadata between work items and GitHub issues.

## Expected Directory Structure

After completing this walkthrough, your repository should look like:

```
workbench-demo/
├── .git/
├── .gitignore
├── .workbench/
│   ├── config.json
│   └── credentials.env (if using local storage)
├── README.md
└── docs/
    ├── README.md
    ├── 00-overview/
    │   └── README.md
    ├── 10-product/
    │   ├── README.md
    │   └── api-authentication-flow.md
    ├── 20-architecture/
    │   └── README.md
    ├── 30-contracts/
    │   └── README.md
    ├── 40-decisions/
    │   ├── README.md
    │   └── use-jwt-tokens-for-api-authentication.md
    ├── 50-runbooks/
    │   └── README.md
    ├── 60-tracking/
    │   └── README.md
    ├── 70-work/
    │   ├── README.md
    │   ├── items/
    │   │   └── TASK-0001-create-user-guide-for-the-api.md
    │   ├── done/
    │   │   └── BUG-0001-fix-authentication-timeout-issue.md
    │   └── templates/
    │       ├── bug.md
    │       ├── task.md
    │       └── spike.md
    └── templates/
        ├── README.md
        ├── adr.md
        ├── contract.md
        ├── feature-spec.md
        └── runbook.md
```

## Common Commands Reference

| Command | Purpose |
|---------|---------|
| `workbench init` | Initialize Workbench in a repository |
| `workbench doctor` | Verify configuration and environment |
| `workbench item new` | Create a new work item |
| `workbench item list` | List work items |
| `workbench item show <ID>` | Show work item details |
| `workbench item status <ID> <status>` | Update work item status |
| `workbench item close <ID>` | Mark work item as done |
| `workbench doc new` | Create a new document |
| `workbench item link <ID>` | Link docs/PRs/issues to a work item |
| `workbench nav sync` | Update navigation and indexes |
| `workbench validate` | Validate work items, links, and schemas |
| `workbench sync` | Full sync (items, docs, nav) |

## Next Steps

- **Read the [Getting Started guide](../../docs/00-overview/getting-started.md)** for more details
- **Explore the [CLI reference](../../docs/30-contracts/cli-help.md)** for all commands
- **Review [ADRs](../../docs/40-decisions/README.md)** to understand design decisions
- **Try the voice features** if you have an OpenAI API key
- **Integrate with your CI/CD** using `workbench validate --strict`

## Troubleshooting

### "Work item ID already exists"

The ID counter is tracked in the file system. If you delete work items, the IDs
won't be reused. This is by design to prevent ID conflicts.

### "Invalid front matter"

Check that your YAML front matter is valid. Use `workbench validate` to see
specific errors.

### "Link validation failed"

Ensure all linked files exist and paths are correct. Use relative paths from the
repository root (e.g., `docs/10-product/my-spec.md`).

## Clean Up

To remove the demo repository:

```bash
cd ..
rm -rf workbench-demo
```
