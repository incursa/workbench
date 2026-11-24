---
artifact_id: SPEC-WB-RESOURCE-PROVIDERS
artifact_type: specification
title: "Provider-neutral resources and configurable authority"
domain: WB
capability: resource-providers
status: approved
owner: platform
related_artifacts:
  - ARC-WB-0009
  - ARC-WB-0010
  - WI-WB-0028
  - WI-WB-0029
  - VER-WB-0010
  - VER-WB-0011
---

# SPEC-WB-RESOURCE-PROVIDERS - Provider-neutral resources and configurable authority

## Purpose

Define the provider-neutral resource boundary that lets Workbench read, enrich,
plan, apply, snapshot, and publish resource sets without embedding
adapter-specific semantics or creating a second writer.

This specification implements the accepted declared-authority and
explicit-transfer direction in the
[`SPEC-001` decision package](../../../../PF-M0.4-workbench-specattest-authority-decision-package.md).
It does not select the deferred SpecAttest Standard tuple and does not claim
cross-tool interoperability or production cutover.

## Scope

- provider-neutral vocabulary and immutable coordinates
- one configured live authority for each resource type/set pair
- optional read-only enrichers and publication targets
- versioned out-of-process stdin/stdout JSON protocol
- capability negotiation, query/get, validation, planning, apply, and health
- idempotency, optimistic concurrency, cancellation, deadlines, and batching
- snapshots, projections, provenance, compatibility pins, publication, and
  explicit authority-transfer coordinates
- compatibility with existing provider-specific Workbench integrations

## REQ-WB-PROVIDER-0001 Shared resource vocabulary

Workbench MUST define stable, provider-neutral meanings for resource,
provider, authority, projection, publication target, and snapshot.

Acceptance criteria:

- a resource has a stable type, set, and identifier
- a provider is an independently versioned executable endpoint
- authority is the sole live normative writer for one type/set pair
- a projection is read-only derived content tied to a source snapshot
- a publication target receives explicit publication changes without becoming
  normative-content authority
- a snapshot is immutable, digest-addressed, versioned, and provenance-bearing

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0002 One declared live authority

Workbench MUST require exactly one enabled live write authority for every
configured resource type/set pair while allowing configured read-only enrichers and
publication targets for that pair.

Acceptance criteria:

- duplicate type/set declarations fail validation
- missing, unknown, disabled, or role-overlapping providers fail validation
- conflict policy is `fail`; newest-write and automatic winner policies are
  rejected
- peer unavailability never changes authority
- wildcard/overlapping scopes and automatic failover are not supported in the
  initial contract

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0003 Provider process isolation

Workbench MUST communicate with resource providers through an explicitly
versioned stdin/stdout JSON protocol without discovering or loading arbitrary
provider DLLs into the Workbench process.

Acceptance criteria:

- providers are launched as configured executables without a shell
- arguments are passed as argument-list entries without shell expansion
- protocol messages are one JSON value per line and carry protocol version,
  request ID, operation, required deadline, idempotency key, and typed body
- stdout is reserved for protocol messages; bounded stderr is retained for
  transport diagnostics
- cancellation or timeout terminates the provider process tree

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0004 Versioned capability and operation contract

Protocol version `workbench.resource-provider.v1` MUST support handshake and
capability negotiation, query, batch get, batch validation, plan changes,
apply changes, and health.

Acceptance criteria:

- handshake completes before non-health operations
- capabilities declare provider identity/version, protocol versions,
  operations, resource types, roles, maximum batch size, batch atomicity and
  ordering, idempotency, optimistic concurrency, and offline snapshots
- clients reject unadvertised operations, roles, protocol versions, and
  oversized or empty batches before dispatch
- clients resolve provider role and exact resource type/set scope from the
  validated authority configuration; a caller cannot self-assert authority
- providers return structured diagnostics with stable codes, severity, target,
  retryability, and optional data

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0005 Safe two-phase mutation

Normative or publication mutations MUST use plan then apply, fail on stale
coordinates, and remain explicit and retry-safe.

Acceptance criteria:

- planning names the expected authority epoch and every update, delete, or
  authority transfer names an expected version token
- plans are immutable and digest-addressed and require explicit approval
- apply names the plan ID/digest, expected authority epoch, stable idempotency
  key, resource type/set, approver, and approval reference
- the envelope and body idempotency keys match
- same-key/same-payload retries can return `alreadyApplied`; changed payload
  under a reused key is a provider conflict
- read-only enrichers cannot plan or apply; publication targets can plan only
  publication changes

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0006 Immutable coordinates and provenance

Resources and snapshots MUST preserve payload digests, version tokens,
authority epochs, compatibility pins when applicable, and provenance across
provider boundaries.

Acceptance criteria:

- snapshots distinguish authoritative from imported or derived state
- baselines are immutable named pointers to exact snapshot IDs and digests,
  not new writers
- projections retain source snapshot, digest, provider, media type, and
  provenance and are never considered normative content
- compatibility pins are explicit repository/revision/schema/digest/
  canonicalization/profile tuples; no default floating SpecAttest pin is
  invented
- authority transfers retain old/new providers, epochs, source/target
  versions, freeze time, payload digest, approver/reference, and rollback
  window
- publication remains an explicit change against a publication-target role;
  its receipt retains source snapshot/digest, immutable manifest digest, and
  permanent reference

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0007 Offline store-and-forward and bounded failure

Provider unavailability MUST leave the declared authority unchanged while
supporting immutable snapshot/package store-and-forward semantics.

Acceptance criteria:

- configured offline policy is `store-and-forward`
- a deadline is carried on each request and enforced by client and host
- incomplete, timed-out, cancelled, invalid, or stale operations cannot be
  represented as applied
- health is callable before handshake for readiness diagnostics
- cancellation tokens flow through every provider operation

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0008 First-party parity and compatibility

First-party local providers MUST implement the same provider interface and
wire protocol as other providers; no local provider may bypass authority,
planning, provenance, or process-isolation checks, and existing GitHub and
execution-observation integrations continue to work without provider
configuration until an explicitly scoped adapter-specific migration.

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0028`](../../work-items/WB/WI-WB-0028-provider-neutral-resource-authority-foundation.md)
- Verified By:
  - [`VER-WB-0010`](../../verification/WB/VER-WB-0010-provider-neutral-resource-authority-foundation.md)

## REQ-WB-PROVIDER-0009 CSV authority and projection safety

The first-party CSV provider MUST distinguish normative authority from editable
projection, preserve spreadsheet-compatible content, and require explicit,
conflict-free reconciliation.

Acceptance criteria:

- authority mode self-identifies as the configured authority and uses immutable
  plans, optimistic version tokens, a cross-process apply lock, and durable
  idempotency receipts
- projection mode names a distinct external authority, cannot plan or apply
  normative changes, and treats only explicit `upsert` or `delete` operations
  as proposals
- missing projection rows never imply deletion
- retries return the snapshot recorded for the original apply even after later
  successful mutations
- work-item and atomic specification rows round-trip commas, quotes, Unicode,
  and embedded newlines under the versioned row schemas

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0029`](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md)
- Verified By:
  - [`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md)

## REQ-WB-PROVIDER-0010 SpecAttest read-only interchange safety

The first-party SpecAttest adapter MUST remain a read-only enricher until its
upstream contracts supply exact-pin, staged, atomic, optimistic-concurrency,
idempotency, and provenance guarantees for mutation.

Acceptance criteria:

- configuration binds one exact set, project, declared authority provider and
  epoch, authority mode, and complete non-floating Standard pin
- Manager exports are marked unattested when exact-pin parity evidence is not
  present and cannot be accepted as authority transitions or publications
- interchange validation rejects unsupported transitions, invalid scope or
  authority, pin mismatch, unsafe stable IDs, missing or malformed retired-ID
  ledgers, and payload or entry digest mismatch
- deterministic ZIP packages validate entry paths before writing and preserve
  immutable source, audit, evidence, and external-reference coordinates
- the adapter does not advertise or emulate plan/apply

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0029`](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md)
- Verified By:
  - [`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md)

## REQ-WB-PROVIDER-0011 ClickUp execution mutation safety

The first-party ClickUp provider MUST expose only explicitly mapped execution
fields while failing closed around scope, stale state, ambiguous creates, and
remote mutation limitations.

Acceptance criteria:

- every direct task get used for read, plan, reconciliation, or apply verifies
  membership in the configured List
- canonical descriptions, specifications, architecture, evidence, and
  provenance links are never synchronized to task descriptions
- read-only, plan, and apply modes advertise only their supported operations;
  apply uses immutable single-task plans, approval, authority epoch, durable
  receipts, and a cross-process state lease
- create uses a stable custom-field reference and reconciliation-only retries
  after an ambiguous POST; delete archives and never calls remote task deletion
- version tokens and a final re-read detect stale plans while documentation
  discloses that ClickUp offers no conditional update eliminating the residual
  compare-before-write race

Trace:

- Satisfied By:
  - [`ARC-WB-0009`](../../architecture/WB/ARC-WB-0009-provider-neutral-resource-authority.md)
  - [`ARC-WB-0010`](../../architecture/WB/ARC-WB-0010-declared-authority-and-process-isolation-decision.md)
- Implemented By:
  - [`WI-WB-0029`](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md)
- Verified By:
  - [`VER-WB-0011`](../../verification/WB/VER-WB-0011-first-party-resource-provider-adapters.md)

## Deferred adapter work

Additional providers must separately declare supported resource types and
roles, implement this protocol in their own executable, and add
provider-specific trace and verification. The exact SpecAttest Standard tuple,
write transitions, and production cutover remain deferred.
