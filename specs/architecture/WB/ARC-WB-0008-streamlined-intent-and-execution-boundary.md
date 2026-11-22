---
artifact_id: ARC-WB-0008
artifact_type: architecture
title: "Streamlined intent and execution boundary"
domain: WB
status: draft
owner: platform
satisfies:
  - REQ-WB-STREAM-0001
  - REQ-WB-STREAM-0002
  - REQ-WB-STREAM-0003
  - REQ-WB-STREAM-0004
  - REQ-WB-STREAM-0005
  - REQ-WB-STREAM-0006
  - REQ-WB-STREAM-0007
  - REQ-WB-STREAM-0008
  - REQ-WB-STREAM-0009
related_artifacts:
  - SPEC-WB-STREAMLINED-WORKFLOW
  - WI-WB-0026
  - VER-WB-0009
---

# ARC-WB-0008 - Streamlined intent and execution boundary

## Purpose

Define how Workbench preserves durable engineering intent without becoming a
competing execution tracker.

## Requirements Satisfied

- [`REQ-WB-STREAM-0001`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0002`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0003`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0004`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0005`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0009`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

## Design Summary

Workbench owns engineering intent: requirements, architecture, constraints,
traceability, and durable verification evidence. GitHub owns PR and issue
execution state. Release and deployment systems own delivery state. Workbench
may reconcile those external facts into local evidence, but it does not create
an independent status that users must manually keep synchronized.

Workflow policy is proportional:

- `light`: work item optional, core validation, intended for trivial changes
  and emergency hotfixes.
- `standard`: durable work item expected for meaningful changes, core
  validation by default, generated views maintained automatically.
- `governed`: explicit requirement, architecture, work-item, and verification
  trace with traceable or auditable validation.

Existing repositories that omit policy configuration resolve to `standard`.
No current artifact field or status changes meaning in this migration.

## Prioritized Delivery Plan

1. Add the optional workflow-policy model and read-only `workbench context`
   command so people and agents receive one exact next action.
2. Add local-only, idempotent `workbench finish`, recording completion evidence
   in existing compatible body sections, refreshing derived navigation, and
   validating without contacting external systems by default.
3. Split pure generated-view refresh from backlink repair and external sync;
   invoke the pure refresh automatically from orchestration commands.
4. Add `doctor --fix` for scaffolding, normalization, and generated-view repair;
   refuse ambiguous or external repairs.
5. Add provider-backed read-only reconciliation for PR merge, release, and
   deployment facts, with explicit authority configuration.
6. Add initiative and parent-child references through a separately reviewed
   schema-compatible extension.
7. Consolidate CI into one composable entry point and remove duplicated jobs
   only after equivalence is demonstrated.

## Tool Defects Versus Policy Defects

Tool defects:

- no single finish operation
- no concise agent context or exact next action
- generated navigation requires explicit maintenance
- doctor reports problems but cannot repair deterministic local drift
- canonical work-item PR linking currently discards the URL
- no read model for external merge, release, or deployment state

Policy defects:

- guidance treats a work item as mandatory for every meaningful change
- the governed workflow is presented as the universal default
- completion is modeled as manual local status reconciliation
- separate CI surfaces repeat equivalent validation work

The first delivery phase changes policy vocabulary and adds read-only context;
it does not silently relax existing validation or mutate existing artifacts.

## Data and State Considerations

- Current Markdown and JSON artifact formats remain valid.
- `Workflow.Level` is optional and defaults to `standard`.
- New JSON command contracts are versioned and use repo-relative paths.
- Derived indexes remain reproducible and should not be treated as authored
  state.
- External synchronization is read-only or explicit; no network call is part
  of an automatic local repair.

## External Execution Reconciliation Contract

The first reconciliation slice is an explicit read model, not another sync
engine. A provider returns immutable observations with this logical shape:

```text
ExecutionObservation(
  provider, kind, externalReference, url,
  normalizedState, nativeState, observedAtUtc, revision)
```

`kind` is `pull_request`, `release`, or `deployment`. Normalized states are
deliberately small: `pending`, `active`, `succeeded`, `failed`, `cancelled`, and
`unknown`; the provider-native value is retained so no information is hidden.
The provider interface has one read operation over configured references and
does not expose create, update, retry, cancel, or deploy methods.

The explicit `workbench execution refresh` command resolves the
configured references and writes a versioned snapshot to
`artifacts/execution-state/<work-item-id>.json`. `workbench execution show`
will read the snapshot without network access unless refresh is requested. A
snapshot records both successful observations and refresh diagnostics, so a
provider outage is not confused with a failed deployment. Canonical work-item
status is never derived from this snapshot.

The first adapter should use the existing GitHub authority and credentials for
pull-request and GitHub release/deployment observations. Provider-neutral
records and tests precede that adapter, so another deployment system can be
added without changing the snapshot contract. This slice requires its own CLI
specification and verification artifact before implementation.

## Initiative and Parent-Child Extension

Schema version 2 adds optional `parent_ref` and `initiative_refs`
fields to canonical work items. A reference is either a local canonical
artifact ID or a cross-repository URI in this form:

```text
workbench://<host>/<owner>/<repository>/<artifact-id>
```

`parent_ref` is singular; `initiative_refs` is an ordered, duplicate-free
list. Local validation rejects self-reference, cycles, and references to a
non-work-item artifact. Remote references are syntactically validated offline;
an explicit resolver may later confirm existence but must not copy remote
status or content. Derived navigation may show the relationships, while the
authored fields remain the source of truth.

This extension lands separately from execution reconciliation. Existing work
items require no migration: both fields are optional, omitted fields load as
no parent and no initiatives, and no existing field changes meaning. Schema
version 2, Markdown/JSON round-trip coverage, local cycle tests, and generated
navigation verify that compatibility boundary.

## Edge Cases and Constraints

- A repeated finish or repair command must be a no-op when state is current.
- Finish dry-runs execute against an isolated repository mirror so generated
  navigation and validation observe the prospective completed item without
  writing to the source repository.
- A validation failure after a local finish must report what changed and the
  exact remaining action rather than hide the completed mutations.
- Existing canonical schemas reject arbitrary execution fields. Until the
  standard adds an external-reference extension, completion evidence belongs
  in existing body sections or a derived execution-state store.
- Emergency work must be able to select `light` policy without rewriting prior
  repository history.

## Alternatives Considered

- Make Workbench the execution source of truth: rejected because it duplicates
  GitHub and deployment systems.
- Remove work items entirely: rejected because durable intent and handoff
  context are valuable for meaningful work.
- Change all canonical formats in the first release: rejected because it would
  break existing repositories and obscure the workflow improvements.

## Risks

- Automatic maintenance could accidentally introduce network latency; all
  automatic paths must explicitly disable external sync.
- Policy names could become enforcement switches prematurely; initial policy
  resolution is informational and orchestration-aware.
- Reconciliation adapters may overfit GitHub; provider-neutral state models
  must precede additional integrations.

## Open Questions

- Whether a future remote resolver should support aliases for moved
  repositories while retaining the original canonical URI.
- Whether initiative references should eventually allow non-work-item
  artifacts; the first schema revision will not.
