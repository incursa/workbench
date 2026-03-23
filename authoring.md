# Authoring Guide

This guide helps people and agents choose the right artifact family and keep
the repo aligned to the canonical Spec Trace model.

## Choose The Artifact

### Specification

Use a specification when you need to define one or more related requirements
for a capability, behavior area, interface, or narrow technical concern.

Read first:

- `overview.md`
- `layout.md`
- `specs/templates/spec-template.md`

### Requirement

Use a requirement when you need one atomic normative statement. Requirements
live inside a specification.

Read first:

- the owning `SPEC-...` file
- `overview.md`
- `specs/templates/spec-template.md`

### Architecture

Use an architecture artifact when you need to explain how requirements will be
satisfied, including rationale and tradeoffs.

Read first:

- the relevant `SPEC-...` file
- `specs/templates/architecture-template.md`

### Work Item

Use a work item when you need to describe implementation work, delivery scope,
and trace links back to requirements and design inputs.

Read first:

- the relevant `SPEC-...` file
- the relevant architecture artifact
- `specs/templates/work-item-template.md`

### Verification Artifact

Use a verification artifact when you need to record how requirements were
proven and what the shared outcome was.

Read first:

- the relevant `SPEC-...` file
- the relevant architecture and work-item artifacts
- `specs/templates/verification-template.md`

## Workflow

1. Start with the authoritative `SPEC-...` files for the task.
2. Open the matching template.
3. Draft or revise the artifact.
4. Run `workbench validate`.
5. Refresh generated views when the repo surface changes.

## Authoring Rules

- Keep requirement clauses atomic.
- Keep `Notes` informative only.
- Use explicit trace labels instead of loose prose when traceability matters.
- Prefer clickable Markdown links for repository-local references.
