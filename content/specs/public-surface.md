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

The public surface is intentionally small:

- `GET /mcp`
- `POST /mcp`
- `GET /mcp/resource/<uri>`
- the `workbench://file/{path}` resource template
- the `search_docs` tool

Everything else stays internal to the build output and Worker implementation.
The docs page should mirror the generated manifest rather than inventing new
surface area at runtime.
