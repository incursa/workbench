---
title: "Install"
---

---
uri: workbench://install
slug: install
title: Install
summary: Install the dependencies, build the manifests, and start the local docs worker.
kind: guide
group: core
aliases:
  - setup
  - getting-started
relatedUris:
  - workbench://overview
  - workbench://fast-path
  - workbench://update
tags:
  - local-development
  - wrangler
priority: 115
includeInSearch: true
searchKind: guide
---

# Install

Use this page when you need the shortest path from a fresh checkout to a
running docs worker.

From the repository root:

```bash
npm install
npm run build:mcp
```

That sequence installs the MCP dependencies, compiles the markdown manifests,
and bundles the Worker.

To run the docs server locally:

```bash
npm run dev:mcp
```

Open the URL Wrangler prints and browse the generated docs index at `/mcp`.

If you only want to verify the build artifacts, run `npm run build:manifests`
followed by `npm run build:worker`.

After changing markdown content or front matter, run `npm test` to rebuild the
Worker and check the MCP transport and search surface.
