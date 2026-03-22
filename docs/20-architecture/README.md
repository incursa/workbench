---
workbench:
  type: guide
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/20-architecture/README.md"
  path: /docs/20-architecture/README.md
owner: platform
status: active
updated: 2026-03-20
---

# Architecture

System architecture, design guidance, data flows, and major components.

Architecture docs live here as `guide` documents. They explain how the
requirements in `specs` are satisfied by the implementation.
If a repository uses `artifact-id-policy.json`, architecture docs should carry
the matching `artifact_id` and `domain` metadata so Workbench can generate and
validate IDs consistently. The legacy `docs/20-architecture` tree remains only
as a compatibility bridge while the top-level `architecture/` root becomes the
canonical home.

## Include

- System diagrams and component boundaries.
- Data flows, integrations, and dependencies.
- Non-functional requirements and constraints.
- Requirements satisfied by this design.
- Design tradeoffs, alternatives, and constraints.

## Template

- [docs/templates/architecture.md](/docs/templates/architecture.md)
