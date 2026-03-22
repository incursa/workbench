---
artifact_id: ARC-WB-0005
artifact_type: architecture
title: "Quality evidence operating model"
domain: WB
status: proposed
owner: platform
satisfies:
  - REQ-QE-0001
  - REQ-QE-0002
  - REQ-QE-0003
  - REQ-QE-0004
  - REQ-QE-0005
  - REQ-QE-0006
related_artifacts:
  - SPEC-QA-QUALITY-EVIDENCE
  - WI-WB-0015
  - WI-WB-0016
  - WI-WB-0017
  - WI-WB-0018
  - WI-WB-0019
  - WI-WB-0020
  - WI-WB-0021
  - WI-WB-0022
  - VER-WB-0002
---

# ARC-WB-0005 - Quality evidence operating model

## Purpose

Workbench already treats local docs, work items, and generated repo views as
first-class artifacts. What it lacks is a coherent operating model for quality
evidence that can explain the current testing state without collapsing into a
CI dashboard or a single green/red gate.

The critical modeling problem is that authored intent and observed evidence are
different kinds of truth. Mixing them would reward easy green bars, hide known
gaps, and make agent reasoning less trustworthy.

## Requirements Satisfied

- REQ-QE-0001
- REQ-QE-0002
- REQ-QE-0003
- REQ-QE-0004
- REQ-QE-0005
- REQ-QE-0006

## Design Summary

Introduce a repo-native quality evidence subsystem with testing as the first
domain.

V1 rules:

- authored testing intent remains under `quality/`
- observed testing evidence is normalized into generated artifacts under
  `artifacts/quality/testing/`
- `quality-report` compares authored truth and observed truth without replacing
  either one
- Workbench exposes a small command surface centered on `workbench quality sync`
  and `workbench quality show`
- the subsystem manages evidence and summaries, not CI orchestration, policy
  enforcement, or autonomous remediation

The existing `quality/testing-intent.yaml` is the authored-intent anchor for
testing intent in V1. Normalized JSON plus generated Markdown become the
canonical observed record for current testing evidence.

## Key Components

- Authored testing intent stored as repository content
- Observed run and coverage artifacts normalized into generated outputs
- Command surface centered on `quality sync` and `quality show`
- Advisory reporting that compares intent to evidence

## Data and State Considerations

Authored intent and observed evidence are different kinds of truth and must remain separate in both storage and reporting.

## Edge Cases and Constraints

- Missing evidence should be reported explicitly.
- Quality output remains advisory and must not become a merge gate.
- Intentional gaps should remain visible in the report.

## Alternatives Considered

- CI-first dashboards as the primary source of truth: rejected because they are
  external, transient, and weak for agent workflows.
- One merged quality file that mixes expectations with outcomes: rejected
  because it destroys the authored-versus-observed boundary.
- A broad multi-language quality platform from day one: rejected because V1
  needs a narrow .NET-first slice that can ship coherently.

## Risks

- Workbench gains a trustworthy evidence layer that stays consistent with the
  repo-native operating model.
- Reviewers and agents get stable JSON and Markdown artifacts for the current
  testing state.
- The repo gains more generated artifacts, but they are clearly bounded under
  `artifacts/quality/testing/`.
- V1 intentionally leaves policy, trend history, flaky-test analysis, and
  semantic test judgment out of scope.

## Open Questions

- None.
