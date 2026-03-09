---
workbench:
  type: adr
  workItems:
    - TASK-0015
    - TASK-0016
    - TASK-0017
    - TASK-0018
    - TASK-0019
    - TASK-0020
    - TASK-0021
    - TASK-0022
  codeRefs: []
  pathHistory:
    - "/C:/docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md"
  path: /docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md
owner: platform
status: proposed
updated: 2026-03-07
---

# ADR-2026-03-07: Quality evidence operating model

- Status: proposed
- Date: 2026-03-07
- Owner: platform

## Context

Workbench already treats local docs, contracts, work items, and generated repo
views as first-class artifacts. What it lacks is a coherent operating model for
quality evidence that can explain the current testing state without collapsing
into a CI dashboard or a single green/red gate.

The critical modeling problem is that authored intent and observed evidence are
different kinds of truth. Mixing them would reward easy green bars, hide known
gaps, and make agent reasoning less trustworthy.

## Decision

Introduce a repo-native quality evidence subsystem with testing as the first
domain.

V1 rules:

- authored testing intent remains under `docs/30-contracts/`
- observed testing evidence is normalized into generated artifacts under
  `artifacts/quality/testing/`
- `quality-report` compares authored truth and observed truth without replacing
  either one
- Workbench exposes a small command surface centered on `workbench quality sync`
  and `workbench quality show`
- the subsystem manages evidence and summaries, not CI orchestration, policy
  enforcement, or autonomous remediation

The existing `docs/30-contracts/test-gate.contract.yaml` is the compatibility
anchor for authored testing intent in V1. Normalized JSON plus generated
Markdown become the canonical observed record for current testing evidence.

## Alternatives considered

- CI-first dashboards as the primary source of truth: rejected because they are
  external, transient, and weak for agent workflows.
- One merged quality file that mixes expectations with outcomes: rejected
  because it destroys the authored-versus-observed boundary.
- A broad multi-language quality platform from day one: rejected because V1
  needs a narrow .NET-first slice that can ship coherently.

## Consequences

- Workbench gains a trustworthy evidence layer that stays consistent with the
  repo-native operating model.
- Reviewers and agents get stable JSON and Markdown artifacts for the current
  testing state.
- The repo gains more generated artifacts, but they are clearly bounded under
  `artifacts/quality/testing/`.
- V1 intentionally leaves policy, trend history, flaky-test analysis, and
  semantic test judgment out of scope.

## Related specs

- [Feature Spec: Quality Evidence v1 (Testing First)](/docs/10-product/feature-spec-quality-evidence-testing-v1.md)
- [Quality Evidence Model (Testing V1)](/docs/30-contracts/quality-evidence-model.md)

## Related work items

- See `workbench.workItems` in the front matter for the starter backlog linked
  to this ADR.
