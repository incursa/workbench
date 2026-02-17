---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  changeNotes: []
  pathHistory: []
  path: /docs/60-tracking/ai-change-notes.md
owner: platform
status: draft
updated: 2025-12-27
---

# AI change notes for markdown docs

Workbench can append AI-generated change notes to markdown documents so every
doc keeps a lightweight history of updates. The `doc summarize` command
calculates a git diff, asks an AI model to summarize it, and stores the note
in `workbench.changeNotes` inside the document front matter.

## Behavior

- Only markdown files are considered.
- Each summary is tagged with a short hash of the diff. If the same diff hash
  is already present, no new entry is added.
- When AI is not configured, the command reports a warning and exits without
  changing files.

## Configuration

Set one of the following environment variables to enable AI summaries:

- `WORKBENCH_AI_OPENAI_KEY` or `OPENAI_API_KEY`
- `WORKBENCH_AI_MODEL` or `OPENAI_MODEL` (default: `gpt-4o-mini`)
- `WORKBENCH_AI_PROVIDER` (`openai` or `none`; default: `openai`)

Optional overrides:

- `WORKBENCH_AI_SUMMARY_MAX_CHARS` (default: `240`)
- `WORKBENCH_AI_SUMMARY_INSTRUCTIONS` (override the summary prompt)

## Pre-commit hook example

Add a pre-commit hook to keep summaries up to date:

```bash
#!/usr/bin/env bash
set -euo pipefail

workbench doc summarize --staged --update-index
```

Save this as `.git/hooks/pre-commit` and make it executable (`chmod +x
.git/hooks/pre-commit`).
