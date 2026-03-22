---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/templates/README.md"
  path: /docs/templates/README.md
owner: platform
status: template
updated: 2026-03-20
---

# Document Templates

Reusable templates for canonical spec-trace artifacts and related repository
docs. Copy these into the appropriate `docs/` subfolder and adapt them for the
specific change.

## Available templates

- `requirement-spec.md`: canonical product specification template.
- `feature-spec.md`: compatibility alias that points authors to
  `requirement-spec.md`.
- `architecture.md`: canonical architecture or design template.
- `verification.md`: canonical verification artifact template.
- `adr.md`: architecture decision records.
- `runbook.md`: operational procedures and troubleshooting steps.
- `contract.md`: API, schema, or interface contracts.

The canonical templates include the file-level metadata fields Workbench uses
for policy-driven artifact IDs when a repository defines `artifact-id-policy.json`.
Where the templates reference another repository document, they use clickable
Markdown links so the relationship stays navigable in rendered docs.

## Usage

- Copy the template into the right repository folder.
- Replace metadata before publishing.
- Keep the structure intact so the doc schema stays valid.
