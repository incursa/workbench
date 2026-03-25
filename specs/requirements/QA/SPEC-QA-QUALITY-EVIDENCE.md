---
artifact_id: SPEC-QA-QUALITY-EVIDENCE
artifact_type: specification
title: Quality Evidence v1 (Testing First)
domain: QA
capability: quality-evidence
status: draft
owner: platform
related_artifacts:
  - WI-WB-0015
  - WI-WB-0016
  - WI-WB-0017
  - WI-WB-0018
  - WI-WB-0019
  - WI-WB-0020
  - WI-WB-0021
  - WI-WB-0022
  - ARC-WB-0005
---

# SPEC-QA-QUALITY-EVIDENCE - Quality Evidence v1 (Testing First)

## Purpose

Add a repo-native quality evidence subsystem that makes the current testing
state of a repository understandable from local artifacts alone.

V1 stays narrow:

- authored testing intent remains a local contract under `quality/`
- observed testing evidence is normalized into JSON and Markdown under
  `artifacts/quality/testing/`
- Workbench exposes a small command surface centered on `quality sync` and
  `quality show`

## Scope

- normalize test inventory, TRX results, and coverage results
- generate a readable report and stable JSON artifacts
- preserve authored intent separately from observed evidence
- expose sync and show commands without turning Workbench into a CI/CD orchestrator

## Context

The repo needs a way to read current testing posture without scraping raw TRX
or coverage files directly. The existing `test-gate.contract.yaml` is the
authored anchor, but the observed evidence must stay separate so the report can
compare intent to reality instead of replacing it.

## REQ-QE-0001 Preserve authored testing intent as truth
The quality workflow MUST read authored testing intent from the local contract, preserve it as authored truth, and keep it distinct from normalized observed evidence.

Trace:
- Implemented By:
  - [`WI-WB-0015`](../../work-items/WB/WI-WB-0015-expand-test-gate-into-authored-testing-intent.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- keep [`quality/testing-intent.yaml`](../../../quality/testing-intent.yaml) as the authored testing-intent anchor
- link related docs, work items, and files where practical

## REQ-QE-0002 Normalize observed evidence
The quality sync command MUST discover test inventory, ingest TRX results, ingest coverage results, and write normalized JSON artifacts plus a Markdown summary under the quality output directory.

Trace:
- Implemented By:
  - [`WI-WB-0016`](../../work-items/WB/WI-WB-0016-add-normalized-test-inventory-discovery-and-schema.md)
  - [`WI-WB-0017`](../../work-items/WB/WI-WB-0017-add-trx-ingestion-and-test-run-summary-contract.md)
  - [`WI-WB-0018`](../../work-items/WB/WI-WB-0018-add-cobertura-ingestion-and-coverage-summary-contract.md)
  - [`WI-WB-0019`](../../work-items/WB/WI-WB-0019-generate-quality-report-json-and-markdown-summary.md)
  - [`WI-WB-0020`](../../work-items/WB/WI-WB-0020-add-quality-sync-and-show-command-surface.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- keep raw TRX and Cobertura files as inputs, not the canonical record
- preserve source paths and generation metadata in the normalized output

## REQ-QE-0003 Render normalized artifacts for inspection
The quality show command MUST render the latest normalized report or a selected normalized artifact instead of requiring manual parsing of raw files.

Trace:
- Implemented By:
  - [`WI-WB-0020`](../../work-items/WB/WI-WB-0020-add-quality-sync-and-show-command-surface.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- default to the report view
- allow JSON output for machine use

## REQ-QE-0004 Compare intent to evidence
The quality report MUST compare authored truth to observed truth and flag missing run evidence, missing coverage evidence, thresholds below intent, and other detectable gaps.

Trace:
- Implemented By:
  - [`WI-WB-0019`](../../work-items/WB/WI-WB-0019-generate-quality-report-json-and-markdown-summary.md)
  - [`WI-WB-0021`](../../work-items/WB/WI-WB-0021-add-analyzer-evidence-and-changed-file-heuristics.md)
  - [`WI-WB-0022`](../../work-items/WB/WI-WB-0022-add-advanced-evidence-extension-points.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- report missing evidence and thresholds explicitly
- keep intentionally untested areas visible

## REQ-QE-0005 Stay advisory
The quality workflow MUST remain advisory and not turn generated output into merge gating or policy enforcement.

Trace:
- Implemented By:
  - [`WI-WB-0020`](../../work-items/WB/WI-WB-0020-add-quality-sync-and-show-command-surface.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- keep the quality report as evidence for humans and agents
- avoid merge-blocking behavior in the generated report

## REQ-QE-0006 Keep traceable links in the report
The quality report MUST carry links to the related docs, work items, and code refs that explain the evidence and the intended testing scope.

Trace:
- Implemented By:
  - [`WI-WB-0015`](../../work-items/WB/WI-WB-0015-expand-test-gate-into-authored-testing-intent.md)
  - [`WI-WB-0019`](../../work-items/WB/WI-WB-0019-generate-quality-report-json-and-markdown-summary.md)
  - [`WI-WB-0020`](../../work-items/WB/WI-WB-0020-add-quality-sync-and-show-command-surface.md)
  - [`WI-WB-0021`](../../work-items/WB/WI-WB-0021-add-analyzer-evidence-and-changed-file-heuristics.md)
  - [`WI-WB-0022`](../../work-items/WB/WI-WB-0022-add-advanced-evidence-extension-points.md)
- Related:
  - [`ARC-WB-0005`](../../architecture/WB/ARC-WB-0005-quality-evidence-operating-model.md)

Notes:
- prefer local repo links where possible

## Open Questions

- Should the authored testing-intent contract remain `test-gate.contract.yaml`
  through V1, or should Workbench rename it to
  `testing-intent.contract.yaml`?
- Should `quality sync` support optional execution of discovery commands in V1,
  or should it remain a pure ingestion-and-normalization command until a later
  phase?
