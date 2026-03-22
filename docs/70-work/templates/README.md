---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/70-work/templates/README.md"
  path: /docs/70-work/templates/README.md
---

# Templates

Work item templates used by `workbench item new`.

## Available templates

- `work-item.task.md`: standard task work items.
- `work-item.bug.md`: bug reports and fixes.
- `work-item.spike.md`: exploratory work or research spikes.

## Canonical shape

Use the richer canonical body format with:

1. Summary
2. Context
3. Trace Links
4. Implementation notes
5. Acceptance criteria
6. Notes

Trace links should use the canonical labels from the spec-trace standard:

- `Addresses`
- `Uses Design`
- `Verified By`

When a trace link points to another repository document, prefer a clickable
Markdown link so the source relationship stays obvious in rendered docs and reviews.
