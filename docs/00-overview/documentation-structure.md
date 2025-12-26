# Documentation structure

## Purpose
This overview describes how the documentation is organized, who each category serves, and where new material should live. It aligns with the scaffold described in [Workbench spec](/docs/00-overview/workbench-spec.md) and clarifies the intent of each `/docs/*` directory.

## Quick lookup: doc types and canonical paths

| Doc type | Canonical path | Intended audience | Belongs here (examples) |
| --- | --- | --- | --- |
| Overview | `/docs/00-overview/` | Everyone | Vision, scope, and cross-cutting specs (e.g., [workbench-spec](/docs/00-overview/workbench-spec.md)). |
| Product | `/docs/10-product/` | Product, design, engineering | Feature specs, requirements, user journeys (see [Product README](/docs/10-product/README.md)). |
| Architecture | `/docs/20-architecture/` | Engineering | System design, data flow, component boundaries (see [Architecture README](/docs/20-architecture/README.md)). |
| Contracts | `/docs/30-contracts/` | Engineering, integrators | Schemas, CLI/API contracts, interface docs (see [Contracts README](/docs/30-contracts/README.md)). |
| Decisions | `/docs/40-decisions/` | Engineering, stakeholders | ADRs and tradeoff history (see [Decisions README](/docs/40-decisions/README.md)). |
| Runbooks | `/docs/50-runbooks/` | Ops, support, on-call | Operational procedures, troubleshooting, releases (see [Runbooks README](/docs/50-runbooks/README.md)). |
| Tracking | `/docs/60-tracking/` | Delivery, leadership | Milestones, progress notes, delivery status (see [Tracking README](/docs/60-tracking/README.md)). |

## Directory intent and placement guidance

### `/docs/00-overview`
**Audience:** everyone (first stop for orientation)

**Purpose:** High-level summaries, the documentation map, and foundational specs that describe how Workbench is intended to work. The canonical scaffold and workflow description live in [workbench-spec](/docs/00-overview/workbench-spec.md).

**Belongs here:**
- Product vision or scope overviews
- Repository-level documentation maps (like this file)
- Any cross-cutting spec that frames the rest of the docs

### `/docs/10-product`
**Audience:** product, design, engineering

**Purpose:** User-facing requirements and feature-level behavior. The stub [Product README](/docs/10-product/README.md) anchors the category.

**Belongs here:**
- Feature specs and acceptance criteria
- User flows or experience notes
- Roadmap-level requirement statements

### `/docs/20-architecture`
**Audience:** engineering

**Purpose:** System-level design, architecture diagrams, and component boundaries. See [Architecture README](/docs/20-architecture/README.md).

**Belongs here:**
- High-level system diagrams
- Data flow descriptions
- Component responsibilities and interfaces

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
[workbench-spec](/docs/00-overview/workbench-spec.md):
- `owner`: team or individual responsible for the doc.
- `status`: e.g., `draft`, `active`, `deprecated`, `archived`.
- `updated`: ISO date (YYYY-MM-DD).
- `related`: use `related.specs` when a work item references this doc.

Example:
```md
---
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
