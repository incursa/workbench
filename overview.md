# Overview

This repository uses the current local Spec Trace model for authored intent.
The canonical artifact families are specification, architecture, work item, and
verification.

## Core Model

- A specification groups one or more related requirements for a capability,
  behavior area, interface, or narrow technical concern.
- A requirement is the atomic normative statement in the system.
- Architecture artifacts explain how requirements are satisfied.
- Work items describe implementation work.
- Verification artifacts record how requirements were proven.

## Requirement Rules

- Requirement clauses use the approved uppercase keyword set only:
  `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`, `SHOULD NOT`, and
  `MAY`.
- Every canonical requirement clause contains exactly one approved keyword.
- `Trace` and `Notes` are optional.
- `Notes` are informative only and must not carry normative requirements.
- Canonical trace labels are `Satisfied By`, `Implemented By`,
  `Verified By`, `Derived From`, `Supersedes`, `Source Refs`, `Test Refs`,
  `Code Refs`, and `Related`.

## Validation Profiles

- `workbench validate` supports the canonical `core`, `traceable`, and
  `auditable` profiles.
- `core` checks schema, identifier, and approved keyword correctness only.
- `traceable` adds canonical graph resolution plus downstream trace
  completeness checks.
- `auditable` adds verification coverage, reciprocal agreement where a
  reciprocal field exists, and orphan ARC/WI/VER detection.
- Generated quality evidence under `artifacts/quality/testing/` is derived
  output and is not canonical `Verified By` coverage unless explicitly
  projected into a verification artifact.

## Derived Attestation

- `workbench quality attest` produces a current repository snapshot of
  requirement coverage, trace completeness, direct refs, work-item progress,
  verification status, and evidence health.
- The attestation outputs under `artifacts/quality/attestation/` are derived
  reports, not canonical authored artifacts.
- Direct `Test Refs` and `Code Refs` remain direct refs; they are not silently
  converted into canonical downstream trace edges.
- Optional repository-local defaults can live in
  [`quality/attestation.yaml`](quality/attestation.yaml) when a repo wants to
  pin evidence roots, freshness windows, or rollup policy.

## File-Level Metadata

- File-level front matter describes the document as a whole.
- Core metadata stays strict.
- Local extensions may use namespaced `x_...` keys when needed.

## Repository Conventions

- Canonical requirements live in `specs/requirements/`.
- Canonical architecture docs live in `specs/architecture/`.
- Canonical work items live in `specs/work-items/`.
- Canonical verification artifacts live in `specs/verification/`.
- Generated Spec Trace outputs live in `specs/generated/`.
- Canonical templates live in `specs/templates/`.
- Canonical schemas live in `specs/schemas/`.
- Local quality-intent inputs live in `quality/`.
