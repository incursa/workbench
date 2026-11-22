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
5. Run format, build, tests, help-contract verification, and core validation.

## Expected Result

Common workflows require fewer commands while preserving durable intent,
stable automation contracts, and explicit external-system authority.

## Evidence

- Passing: policy and context unit/integration tests.
- Passing: finish idempotence, prospective no-mutation dry-run, structured
  post-mutation failure, terminal-status safety, and JSON contract tests.
- Passing: Markdown-linked trace references resolve identically to plain IDs;
  repository triage reduced traceable findings from 779 to 444 and auditable
  findings from 2,142 to 1,390 without adding synthetic relationships.
- Passing: 13 docs MCP protocol/search/security tests and zero npm audit
  vulnerabilities.
- Passing: doctor JSON contract, local scaffolding repair, established-index
  refresh, idempotence, malformed-config preservation, and optional provider
  readiness checks.
- Planned: provider-backed external reconciliation tests.

## Status

planned

## Related Artifacts

- [`SPEC-WB-STREAMLINED-WORKFLOW`](../../requirements/WB/SPEC-WB-STREAMLINED-WORKFLOW.md)
- [`WI-WB-0026`](../../work-items/WB/WI-WB-0026-streamline-workflow-orchestration.md)
- [`ARC-WB-0008`](../../architecture/WB/ARC-WB-0008-streamlined-intent-and-execution-boundary.md)
