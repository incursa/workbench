---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/10-product/specs/README.md"
  path: /specs/requirements/README.md
owner: platform
status: active
updated: 2026-03-21
---

# Requirements

Requirement specifications for product capabilities live here.

## Use this folder for

- one coherent capability per file
- Markdown requirement specs with stable requirement IDs
- links back to architecture docs, work items, and ADRs

## Canonical specs

- [CLI Onboarding, Init Walkthrough, and Wizard Mode](CLI/SPEC-CLI-ONBOARDING.md)
- [Local Web UI Mode](WEB/SPEC-WEB-LOCAL-UI.md)
- [Quality Evidence v1 (Testing First)](QA/SPEC-QA-QUALITY-EVIDENCE.md)
- [Terminal UI Mode](TUI/SPEC-TUI-TERMINAL-UI.md)
- [Work Item Sync (GitHub Issues + Branches)](SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)

## Notes

- Keep overview-level product prose in `docs/10-product/README.md`.
- Keep design details in `architecture`.
- Keep the spec body parseable so Workbench can normalize and sync it.
- The legacy `docs/10-product/specs` tree is retained only as a migration bridge.
