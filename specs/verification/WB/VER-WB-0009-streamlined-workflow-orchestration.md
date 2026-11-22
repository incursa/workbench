---
artifact_id: VER-WB-0009
artifact_type: verification
title: "Streamlined workflow orchestration"
domain: WB
status: planned
owner: platform
verifies:
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
  - WI-WB-0027
  - ARC-WB-0008
---

# VER-WB-0009 - Streamlined workflow orchestration

## Scope

Workflow policy resolution, agent context, idempotent finish behavior,
automatic local derived-view maintenance, deterministic repairs, stable output
contracts, and migration compatibility.

## Requirements Verified

- [`REQ-WB-STREAM-0001`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0002`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0003`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0004`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0005`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0006`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0007`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0008`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`REQ-WB-STREAM-0009`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)

## Verification Method

Automated unit and integration tests plus repository-level build, format, and
validation checks.

## Preconditions

- The policy, context, and finish increments have landed.

## Procedure or Approach

1. Verify legacy configuration resolves to `standard` without rewriting it.
2. Verify context output is deterministic and recommends exactly one command.
3. Run finish twice and confirm evidence is not duplicated and generated views
   are unchanged on the second run.
4. Verify automatic paths do not contact GitHub.
5. Refresh explicit PR, release, and deployment references through a fake or
   stubbed provider and verify every outbound request is read-only.
6. Make the provider unavailable and verify the snapshot retains prior
   observations with structured age and failure diagnostics.
7. Read the snapshot through `execution show` without credentials or provider
   access.
8. Run format, build, tests, help-contract verification, and core validation.

## Expected Result

Common workflows require fewer commands while preserving durable intent,
stable automation contracts, and explicit external-system authority.

## Evidence

- Passing: policy and context unit/integration tests.
- Passing: finish idempotence, prospective no-mutation dry-run, structured
  post-mutation failure, terminal-status safety, and JSON contract tests.
- Passing: Markdown-linked trace references resolve identically to plain IDs;
  repository triage reduced traceable findings from 779 to 445 and auditable
  findings from 2,142 to 1,387 without adding synthetic relationships. The
  remaining backlog is reproducible by category, domain, and artifact kind via
  `scripts/trace/report-validation-backlog.ps1`.
- Passing: 13 docs MCP protocol/search/security tests and zero npm audit
  vulnerabilities; the same checks now run as a required main CI job through
  `scripts/verify.ps1`.
- Passing: CI trigger and job ownership no longer duplicate feature-branch
  push/PR runs, Ubuntu test execution, or format/analyzer verification.
- Passing: doctor JSON contract, local scaffolding repair, established-index
  refresh, idempotence, malformed-config preservation, and optional provider
  readiness checks.
- Passing: provider-backed PR, release, and deployment observations use GET
  requests only and normalize provider-native state without mutating work-item
  status.
- Passing: schema-versioned snapshots, explicit refresh, offline show, missing
  credential diagnostics, and stale-provider retention with structured prior
  observation timestamp and age.
- Passing: provider code is reachable only from the explicit `execution
  refresh` command; existing automatic local commands have no provider
  dependency or call path.
- Passing: optional hierarchy schema round trips, versioned CLI JSON, offline
  remote URI parsing, local missing/self/cycle rejection in mutation and
  repository validation, derived child/navigation views, and proof that work
  item status is unchanged.

## Status

planned

## Related Artifacts

- [`SPEC-WB-STREAMLINED-WORKFLOW`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- [`WI-WB-0027`](../../work-items/WB/WI-WB-0027-complete-web-editing-and-connected-execution-capabilities.md)
- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
