---
workbench:
  type: doc
  workItems: []
  codeRefs: []
owner: platform
status: active
updated: 2025-12-27
---

# Docs

Documentation for product, architecture, decisions, and operational guidance.

## Organization

The numbered folders group docs by intent, from high-level overview to delivery
tracking. Use the matching README in each section for scope and conventions.

- `docs/00-overview/README.md`: vision, positioning, and summaries.
- `docs/10-product/README.md`: product requirements and user-facing behavior.
- `docs/20-architecture/README.md`: system design and technical architecture.
- `docs/30-contracts/README.md`: schemas, APIs, and CLI contracts.
- `docs/40-decisions/README.md`: ADRs and tradeoff history.
- `docs/50-runbooks/README.md`: operational runbooks and procedures.
- `docs/60-tracking/README.md`: milestones and delivery tracking.
- `docs/templates/README.md`: reusable doc templates.

## Metadata

Most docs should include YAML front matter that follows
`docs/30-contracts/doc.schema.json`. Keep `owner`, `status`, and `updated`
accurate so the CLI and docs tooling can index the content.
