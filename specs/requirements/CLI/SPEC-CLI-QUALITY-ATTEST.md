---
artifact_id: SPEC-CLI-QUALITY-ATTEST
artifact_type: specification
title: "CLI Quality Attest Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-QUALITY
  - SPEC-CLI-SURFACE
  - SPEC-QA-QUALITY-EVIDENCE
  - ARC-WB-0005
  - WI-WB-0025
  - VER-WB-0008
---

# SPEC-CLI-QUALITY-ATTEST - CLI Quality Attest Command

## Purpose

Define the contract for the derived repository evidence attestation snapshot.

## Scope

- `workbench quality attest`

## REQ-CLI-QUALITY-ATTEST-0001 `workbench quality attest`

`quality attest` MUST accept the documented scope, profile, emit, out-dir,
config, results, coverage, benchmarks, manual-qa, exec, and no-exec options
while generating a current derived evidence snapshot without mutating
canonical authored artifacts.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)

## REQ-CLI-QUALITY-ATTEST-0002 Report outputs

`quality attest` MUST write a summary HTML report, a detailed HTML report, and
a machine-readable JSON snapshot beneath the requested output directory when
the corresponding emit mode is enabled.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)

## REQ-CLI-QUALITY-ATTEST-0003 Semantic HTML boundary

The attestation HTML reports MUST be static semantic HTML documents that use
headings, tables, lists, and collapsible details sections without a client-side
application or framework dependency.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)

## REQ-CLI-QUALITY-ATTEST-0004 Derived evidence boundary

`quality attest` MUST report canonical downstream trace separately from direct
implementation refs while keeping tests, code refs, and quality artifacts out
of canonical `Satisfied By`, `Implemented By`, and `Verified By` edges.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)

Notes:
- the derived report may harvest test and code evidence from linked verification
  artifacts' `Evidence` sections without turning those refs into canonical
  downstream trace edges
- verification evidence may mark benchmark coverage as intentionally
  `not-applicable` when a requirement does not need benchmark proof

## REQ-CLI-QUALITY-ATTEST-0005 Explicit execution opt-in

`quality attest` MUST execute configured evidence refresh commands only when
explicitly requested and reject conflicting `--exec` and `--no-exec` options.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)

## REQ-CLI-QUALITY-ATTEST-0006 Machine-readable output

`quality attest` MUST support machine-readable output when requested.

Trace:
- Satisfied By:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)
- Implemented By:
  - [`WI-WB-0025`](../../work-items/WB/WI-WB-0025-derive-repository-attestation-snapshot.md)
- Verified By:
  - [`VER-WB-0008`](../../verification/WB/VER-WB-0008-derived-repository-attestation-snapshot.md)
