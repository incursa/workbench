# Authoring Guide

This guide helps people and agents choose the right artifact family and keep
the repo aligned to the canonical Spec Trace model.

## Choose The Artifact

### Specification

Use a specification when you need to define one or more related requirements
for a capability, behavior area, interface, or narrow technical concern.

Read first:

- [`overview.md`](overview.md)
- [`layout.md`](layout.md)
- [`specs/templates/spec-template.md`](specs/templates/spec-template.md)

### Requirement

Use a requirement when you need one atomic normative statement. Requirements
live inside a specification.

Read first:

- the owning `SPEC-...` file
- [`overview.md`](overview.md)
- [`specs/templates/spec-template.md`](specs/templates/spec-template.md)

### Architecture

Use an architecture artifact when you need to explain how requirements will be
satisfied, including rationale and tradeoffs.

Read first:

- the relevant `SPEC-...` file
- [`specs/templates/architecture-template.md`](specs/templates/architecture-template.md)

### Work Item

Use a work item when you need to describe implementation work, delivery scope,
and trace links back to requirements and design inputs.

Read first:

- the relevant `SPEC-...` file
- the relevant architecture artifact
- [`specs/templates/work-item-template.md`](specs/templates/work-item-template.md)

### Verification Artifact

Use a verification artifact when you need to record how requirements were
proven and what the shared outcome was.

Read first:

- the relevant `SPEC-...` file
- the relevant architecture and work-item artifacts
- [`specs/templates/verification-template.md`](specs/templates/verification-template.md)

### Derived Attestation Snapshot

Use `workbench quality attest` when you need a current snapshot of requirement
coverage, trace completeness, direct refs, work-item progress, verification
status, and evidence health. It reads authored artifacts and derived evidence
but does not create canonical trace or replace authored requirements.

Read first:

- [`overview.md`](overview.md)
- [`quality/attestation.yaml`](quality/attestation.yaml) if the repo defines
  local evidence-root or rollup defaults

## Workflow

1. Start with the authoritative `SPEC-...` files for the task.
2. Open the matching template.
3. Draft or revise the artifact.
4. Run `workbench validate`.
5. Refresh generated views when the repo surface changes.
6. Use `workbench quality attest` when you need the derived evidence snapshot
   instead of another canonical artifact.

When traceability needs stronger enforcement, run `workbench validate` with
`--profile traceable` or `--profile auditable`. Use `--scope` to narrow the
validation target to a subtree without turning derived quality evidence into
canonical trace.

## Authoring Rules

- Keep requirement clauses atomic.
- Keep `Notes` informative only.
- Use explicit trace labels instead of loose prose when traceability matters.
- Prefer clickable relative Markdown links for repository-local references, and keep inline code styling inside the link text when needed, for example [`ValidationService`](src/Workbench.Core/ValidationService.cs).
- Use absolute URLs only for external targets such as NuGet package pages or other web-hosted documentation.
