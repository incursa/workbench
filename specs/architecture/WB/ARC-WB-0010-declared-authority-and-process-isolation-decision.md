---
artifact_id: ARC-WB-0010
artifact_type: architecture
title: "Decision record: declared authority and process-isolated providers"
domain: WB
status: approved
owner: platform
satisfies:
  - REQ-WB-PROVIDER-0002
  - REQ-WB-PROVIDER-0003
  - REQ-WB-PROVIDER-0005
  - REQ-WB-PROVIDER-0008
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
  - ARC-WB-0009
  - WI-WB-0028
  - WI-WB-0029
  - VER-WB-0010
  - VER-WB-0011
---

# ARC-WB-0010 - Decision record: declared authority and process-isolated providers

## Decision ID and status

Repository decision alias: `ADR-WB-PROVIDER-0001`  
Status: accepted  
Decision date: 2026-11-21

Workbench records this decision as an `ARC-*` artifact because the current
canonical artifact policy has no ADR family. The alias is a repository-local
locator and does not redefine the SpecAttest Standard.

## Context

Workbench needs CSV, tracker, publication, specification-management, and local
repository integrations without hard-coding every system into its core.
Provider extensibility could otherwise create two independent hazards:

- multiple tools may believe they can write the same resource set
- arbitrary adapter assemblies may execute inside Workbench with its trust and
  dependency graph

The portfolio owner accepted the declared-authority, explicit-transfer model
in the
[`SPEC-001` decision package](../../../../PF-M0.4-workbench-specattest-authority-decision-package.md).
Its Standard tuple remains explicitly deferred.

## Decision

1. Every configured resource type/set pair has exactly one enabled live
   normative-content authority.
2. Other configured roles are read-only enricher or publication target; they
   cannot become content authority through capability advertisement or peer
   failure.
3. Authority changes only through a frozen, digest-pinned, explicitly approved
   transfer record. Export, staging, projection, publication, or retry does not
   transfer authority.
4. Conflicts fail closed. Workbench does not implement last-write-wins,
   automatic field merges, automatic failover, or dual-write adapters.
5. Providers execute out of process and communicate through the explicitly
   versioned NDJSON stdin/stdout protocol. Workbench does not load arbitrary
   provider DLLs.
6. First-party local providers implement `IResourceProvider` and expose it
   through `ResourceProviderProtocolHost` in their own executable. They receive
   no hidden in-process bypass.
7. Mutations use plan/apply, optimistic version tokens, authority epochs,
   immutable plan digests, idempotency keys, explicit approval, and structured
   diagnostics.
8. Existing GitHub-specific abstractions remain compatibility surfaces until
   separately migrated; the generic foundation does not change their behavior.

## Consequences

Positive:

- authority is reviewable in repository configuration
- adapter crashes and dependency conflicts are isolated from Workbench
- providers can be implemented in any language that honors the schema
- local and remote-backed adapters share cancellation, batching, concurrency,
  and diagnostic semantics
- the accepted SpecAttest pins, digests, provenance, snapshot, publication,
  offline, and transfer rules can be represented without adapter-specific core
  types

Costs:

- each provider requires an executable host and protocol conformance tests
- process startup and JSON framing add overhead
- durable idempotency and optimistic concurrency remain provider
  implementation responsibilities
- credentials require a separate secure injection policy; they are not stored
  in provider configuration

## Rejected alternatives

- Load provider assemblies with reflection or a dependency-injection plugin
  catalog.
- Let provider capabilities determine authority dynamically.
- Select the newest resource by timestamp when providers disagree.
- Promote an enricher or publication target during authority outage.
- Treat existing in-process GitHub implementations as conforming generic
  providers without a process boundary.

## Follow-up

The first-party CSV, SpecAttest, and ClickUp adapters add their requirements,
executables, conformance fixtures, security boundaries, and evidence through
[`WI-WB-0029`](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md)
and
[`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md).
Future adapters must do the same. SpecAttest remains blocked from write
interoperability and cutover claims until the owner selects the P0 Standard
tuple and the later parity gates pass.
