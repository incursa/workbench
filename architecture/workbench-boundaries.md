---
workbench:
  type: guide
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/architecture/workbench-boundaries.md"
  path: /architecture/workbench-boundaries.md
owner: platform
status: draft
updated: 2026-03-21
---

# Workbench Boundaries

## Purpose

Describe the top-level product boundaries that Workbench owns.

## Scope

- spec-trace integration
- CLI command surface
- browser-based web UI
- shared validation and storage services

## System shape

Workbench is a single product with multiple surfaces:

- the standards integration layer defines how Workbench reads, writes, and
  validates spec-trace artifacts
- the CLI defines the automation-first command model
- the web UI defines the browser-first interactive model
- shared services own parsing, mutation, validation, and repo discovery

## Boundary model

### Standards integration

This layer consumes the canonical spec-trace contracts and converts them into
Workbench behavior. It does not redefine the standard.

### CLI surface

The CLI is the primary automation and scripting surface. It should expose the
core command groups and remain thin over shared services.

### Web surface

The web UI is the pointer-first browsing and editing surface. It should reuse
the same shared services so behavior stays consistent with the CLI.

### Shared services

Shared services should handle:

- repository and workspace detection
- front matter parsing and normalization
- spec and work-item read/write operations
- identifier allocation
- validation and sync logic

## Next-step gaps

- Which operations must be blocking errors versus warnings?
- Which workflows belong only in the CLI for now?
- Which workflows should be available in both the CLI and web UI from day one?
