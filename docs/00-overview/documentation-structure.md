---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/00-overview/documentation-structure.md"
  path: /docs/00-overview/documentation-structure.md
owner: platform
status: active
updated: 2026-03-20
---

# Documentation structure

## Purpose
This overview describes how the documentation is organized, who each category serves, and where new material should live. It aligns with the repo standard in [Specification and traceability standard](/docs/00-overview/specification-and-traceability-standard.md) and clarifies the intent of the canonical `specs/`, `architecture/`, and `work/` roots alongside the legacy `/docs/*` tree.

## Quick lookup: doc types and canonical paths

| Doc type | Canonical path | Intended audience | Belongs here (examples) |
| --- | --- | --- | --- |
| Overview | `/docs/00-overview/` | Everyone | Vision, scope, and cross-cutting standards (e.g., [specification-and-traceability-standard](/docs/00-overview/specification-and-traceability-standard.md)). |
| Product | `/docs/10-product/` | Product, design, engineering | Feature specs, requirements, user journeys, and product notes (see [Product README](/docs/10-product/README.md)). |
| Requirements | `/specs/` | Product, engineering | Canonical requirement specifications and related spec indexes (see [Requirements README](/specs/README.md)). |
| Architecture | `/architecture/` | Engineering | System design, data flow, component boundaries, and design guidance (see [Architecture README](/architecture/README.md)). |
| Work | `/work/` | Delivery, engineering | Active and closed work items plus templates (see [Work README](/work/README.md)). |
| Contracts | `/docs/30-contracts/` | Engineering, integrators | Schemas, CLI/API contracts, interface docs (see [Contracts README](/docs/30-contracts/README.md)). |
| Decisions | `/docs/40-decisions/` | Engineering, stakeholders | ADRs and tradeoff history (see [Decisions README](/docs/40-decisions/README.md)). |
| Runbooks | `/docs/50-runbooks/` | Ops, support, on-call | Operational procedures, troubleshooting, releases (see [Runbooks README](/docs/50-runbooks/README.md)). |
| Tracking | `/docs/60-tracking/` | Delivery, leadership | Milestones, progress notes, delivery status (see [Tracking README](/docs/60-tracking/README.md)). |
| Templates | `/docs/templates/` | Authors | Reusable doc templates for specs, architecture, ADRs, contracts, and runbooks. |

## Directory intent and placement guidance

### `/docs/00-overview`
**Audience:** everyone (first stop for orientation)

**Purpose:** High-level summaries, the documentation map, and foundational standards that describe how Workbench documentation is organized. The canonical repo standard lives in [specification-and-traceability-standard](/docs/00-overview/specification-and-traceability-standard.md).

**Belongs here:**
- Product vision or scope overviews
- Repository-level documentation maps (like this file)
- Any cross-cutting spec that frames the rest of the docs

### `/docs/10-product`
**Audience:** product, design, engineering

**Purpose:** User-facing requirements and feature-level behavior. Requirement specs live under [specs](/specs/README.md), and the category is anchored by [Product README](/docs/10-product/README.md).

**Belongs here:**
- Feature specs and acceptance criteria
- User flows or experience notes
- Roadmap-level requirement statements

### `/architecture`
**Audience:** engineering

**Purpose:** System-level design, architecture diagrams, and component boundaries. See [Architecture README](/architecture/README.md).

**Belongs here:**
- High-level system diagrams
- Data flow descriptions
- Component responsibilities and interfaces

### `/work`
**Audience:** delivery, engineering

**Purpose:** Active and closed work items plus the templates used to create them. See [Work README](/work/README.md).

**Belongs here:**
- Active work items
- Closed work items
- Work-item templates

### `/docs/30-contracts`
**Audience:** engineering, integrators

**Purpose:** Interface definitions and machine-readable contracts. The [Contracts README](/docs/30-contracts/README.md) lists current schema artifacts.

**Belongs here:**
- JSON schemas (e.g., work item and config schema)
- CLI help/contract docs
- API/interface definitions

### `/docs/40-decisions`
**Audience:** engineering, stakeholders

**Purpose:** The history of architectural decisions, tradeoffs, and rationale. See [Decisions README](/docs/40-decisions/README.md).

**Belongs here:**
- ADRs that capture options, tradeoffs, and chosen direction
- Decision logs that explain why a path was selected

### `/docs/50-runbooks`
**Audience:** ops, support, on-call

**Purpose:** Operational guidance and step-by-step procedures. See [Runbooks README](/docs/50-runbooks/README.md).

**Belongs here:**
- Troubleshooting guides
- Release/checklist procedures
- Operational playbooks

### `/docs/60-tracking`
**Audience:** delivery, leadership

**Purpose:** Progress tracking, milestones, and delivery notes. See [Tracking README](/docs/60-tracking/README.md).

**Belongs here:**
- Milestone summaries
- Status updates
- Delivery retrospectives or timelines

## Documentation lifecycle

### Ownership expectations
- Every doc should have a named owner (team or individual) responsible for accuracy.
- Owners are accountable for updates when related behavior, contracts, or workflows change.
- If a doc has no clear owner, assign one before merging changes that depend on it.

### Review cadence
- Review docs at least once per release or quarterly (whichever comes first).
- Prioritize reviews for docs referenced by active work items or recent releases.

### Deprecation and archival
- Mark deprecated docs with a clear status and a pointer to the replacement.
- Move archived docs to `/docs/60-tracking/archived/` and add a short note in the
  original location linking to the archive entry.
- Keep archived docs read-only except for annotation metadata.

### Metadata conventions (front matter)
Documentation can use optional YAML front matter aligned with work item conventions in
[specification-and-traceability-standard](/docs/00-overview/specification-and-traceability-standard.md):
- `owner`: team or individual responsible for the doc.
- `status`: e.g., `draft`, `active`, `deprecated`, `archived`.
- `updated`: ISO date (YYYY-MM-DD).
- `related`: use `related.specs` when a work item references this doc.

Example:
```md
---
workbench:
  type: doc
  workItems: []
  codeRefs: []
owner: platform
status: active
updated: 2025-01-15
related:
  specs:
    - /docs/00-overview/documentation-structure.md
---
```

### Work item documentation checklist
- Link specs/ADRs in the work item front matter (`related.specs`, `related.adrs`).
- Create or update docs when:
  - new user-facing behavior is introduced,
  - contracts, schemas, or interfaces change,
  - runbooks or operational procedures change.
- Add or refresh doc front matter (`owner`, `status`, `updated`) when changes land.
- Confirm archived/deprecated docs are moved and referenced correctly.
