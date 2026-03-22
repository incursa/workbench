---
workbench:
  type: specification
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /specs/README.md
---

# Specs

Canonical specifications live here. Keep them directly under `specs/` and use the sibling top-level `architecture/` and `work/` roots for design and execution artifacts.

## Layout

- `specs`: specification documents.
- `architecture`: architecture and design documents grouped by domain.
- `work`: work items, backlog, and workboard files.
- `overview`: high-level product and orientation docs.
- `contracts`: CLI, API, and schema contracts.
- `decisions`: ADRs and tradeoff history.
- `runbooks`: operational procedures and playbooks.
- `tracking`: milestone and delivery notes.

## Workflow

1. Author or update canonical artifacts in the appropriate family directory.
2. Keep trace links in front matter and body sections aligned with the canonical schema.
3. Run `workbench validate` and `workbench nav sync` after changes.
