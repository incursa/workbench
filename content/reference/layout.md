---
title: "Layout"
---

---
uri: workbench://reference/layout
slug: layout
title: Layout
summary: The repository layout used by the Workbench docs MCP server.
kind: reference
group: reference
aliases:
  - repository-layout
  - file-map
  - structure
relatedUris:
  - workbench://overview
  - workbench://guides/authoring
  - workbench://specs/public-surface
tags:
  - repository
  - layout
  - generated-output
priority: 96
includeInSearch: true
searchKind: reference
---

# Layout

The docs server keeps authored content and generated output separate.

Use this map to find the source tree that the docs site publishes and the
runtime output that `npm test` rebuilds.

```text
content/
  overview.md
  install.md
  fast-path.md
  update.md
  guides/
    authoring.md
    search.md
  reference/
    layout.md
  specs/
    public-surface.md
    verification-index.md
  ai/
    llms-txt.md
dist/mcp/
  manifest.json
  resources.json
  search-index.json
  worker.mjs
scripts/
  generate-mcp.mjs
  build-mcp.mjs
src/mcp/
  worker.ts
tests/mcp/
  transport.test.mjs
  tools.test.mjs
```

Keep `dist/mcp/` generated and treat the docs tree as the source of truth.
The docs site mirror uses `content/` as input and does not read `dist/mcp/` at
authoring time.

Root files worth knowing about:

- `README.md`
- `docs.site.json`
- `runbooks/`
- `specs/`
