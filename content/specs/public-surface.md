---
title: "Public Surface"
---

---
uri: workbench://specs/public-surface
slug: public-surface
title: Public Surface
summary: The stable MCP endpoints, resource template, and search tool exposed by the docs server.
kind: spec
group: specs
aliases:
  - public-api
  - surface
  - transport-contract
relatedUris:
  - workbench://reference/layout
  - workbench://specs/verification-index
tags:
  - mcp
  - api
  - contract
priority: 90
includeInSearch: true
searchKind: spec
---

# Public Surface

This page defines the stable docs MCP contract that the Worker exposes to
readers and tools.

The public surface is intentionally small:

- `GET /mcp`
- `POST /mcp`
- `GET /mcp/resource/<uri>`
- the `workbench://file/{path}` resource template
- the `search_docs` tool

Everything else stays internal to the build output and Worker implementation.
The docs page should mirror the generated manifest rather than inventing new
surface area at runtime.

If you change the public surface, update the docs source under `content/`,
rebuild with `npm test`, and keep the mirror workflow in
[`docs.site.json`](../../docs.site.json) aligned with the source tree.
