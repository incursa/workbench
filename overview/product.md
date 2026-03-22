---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/overview/product.md"
  path: /overview/product.md
owner: platform
status: active
updated: 2026-03-20
---

# Product

Product requirements, explicit specs, and user-facing behavior.

## Overview docs

- Product summaries, release framing, and other cross-cutting product notes.
- Keep overview-style content here when it is not itself a requirement spec.

## Requirements specs

- Canonical home: [specs](/specs/README.md)
- Use [templates/requirement-spec.md](/templates/requirement-spec.md)
- Policy-driven spec IDs live in the spec front matter as `artifact_id`,
  `domain`, and `capability`. If a repository adds `artifact-id-policy.json`,
  Workbench will generate matching IDs from those fields.
- [`feature-spec.md`](/templates/feature-spec.md) remains a compatibility alias for older authoring habits.
- The old product-specs folder is gone; canonical specs now live under `specs/`.

## Feature-level notes

- User journeys, UX notes, capability summaries, and other prose that supports a spec.
- If the document states a requirement, it belongs in `specs`.

## Include

- User stories and acceptance criteria.
- Requirement blocks with stable IDs.
- UX notes or copy guidance that define behavior.
- Release-impacting changes for PM and design review.
