---
uri: workbench://guides/authoring
slug: authoring
title: Authoring Guide
summary: How to write markdown docs for the Workbench docs MCP server.
kind: guide
group: guides
aliases:
  - authoring
  - content-model
  - front-matter
relatedUris:
  - workbench://overview
  - workbench://guides/search
  - workbench://reference/layout
tags:
  - markdown
  - front-matter
  - validation
priority: 100
includeInSearch: true
searchKind: guide
---

# Authoring Guide

Use one markdown file per topic.

Front matter carries the metadata the generator needs:

- `uri`
- `slug`
- `title`
- `summary`
- `kind`
- `group`
- `aliases`
- `relatedUris`
- `tags`
- `priority`
- `includeInSearch`
- `searchKind`

The body should hold the actual documentation. The build validates duplicate
URIs, duplicate slugs within a group, unsupported kinds, and broken
`relatedUris` before it writes `dist/mcp/*.json`.

Keep related URIs internal to the `workbench://` namespace so the docs surface
remains deterministic.
