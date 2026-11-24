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

### CSV Authority Or Editable Projection

Use the first-party CSV provider when a resource set is intentionally authored
in a local spreadsheet-compatible store, or when reviewers need an editable
projection of another provider's immutable snapshot. Read
[`content/reference/csv-provider.md`](content/reference/csv-provider.md) before
editing. A projection remains non-authoritative: mark each intended operation,
review the reconciliation preview, and use the declared authority's explicit
plan/apply workflow.

### SpecAttest Manager Review Snapshot

Use the SpecAttest provider only as a read-only enricher until Manager exposes
the exact-pin, staged, atomic, optimistic-concurrency-protected, idempotent
interchange apply contract required by `SPEC-001`. Read
[`content/reference/specattest-provider.md`](content/reference/specattest-provider.md)
before configuring an endpoint. Exporting or inspecting a Manager snapshot
never transfers authority, and an unattested snapshot cannot be accepted or
published as authoritative content.

### ClickUp Execution Resources

Use the ClickUp provider for operational execution fields only; canonical work
items and trace remain under Workbench authority. Read
[`content/reference/clickup-provider.md`](content/reference/clickup-provider.md)
before enabling `plan` or `apply`. Review the provider's single-task atomicity,
stable create marker, idempotency, archive-only delete, and residual ClickUp
compare-before-write race constraints.

## Workflow

1. Run `workbench context` to resolve the repository's workflow level,
   applicable artifacts, active work, and recommended next command.
2. Under `light`, make trivial or emergency changes without creating a work
   item when durable coordination is unnecessary.
3. Under `standard`, create a work item for meaningful multi-step or handoff
   work. Under `governed`, also maintain explicit requirement, architecture,
   and verification trace.
4. Start with the authoritative `SPEC-...` files when the change affects
   durable behavior or constraints.
5. Open the matching template and draft or revise the artifact.
6. Use `workbench finish <id>` for tracked work so completion evidence,
   generated views, and validation are reconciled together. Run `workbench doc
   sync` separately when the change also requires backlink or document repair.
7. Run `workbench validate` directly for untracked light changes or focused
   diagnostics.
8. Use `workbench quality attest` when you need the derived evidence snapshot
   instead of another canonical artifact.

When traceability needs stronger enforcement, run `workbench validate` with
`--profile traceable` or `--profile auditable`. Use `--scope` to narrow the
validation target to a subtree without turning derived quality evidence into
canonical trace.

GitHub and deployment systems remain authoritative for execution state.
Workbench records engineering intent, architectural decisions, traceability,
and durable verification evidence; it should not require a parallel manual
status update when execution facts can be reconciled from their source.

## Authoring Rules

- Keep requirement clauses atomic.
- Keep `Notes` informative only.
- Use explicit trace labels instead of loose prose when traceability matters.
- Prefer clickable relative Markdown links for repository-local references, and keep inline code styling inside the link text when needed, for example [`ValidationService`](src/Workbench.Core/ValidationService.cs).
- Use absolute URLs only for external targets such as NuGet package pages or other web-hosted documentation.
