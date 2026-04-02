---
uri: workbench://fast-path
slug: fast-path
title: Fast Path
summary: The shortest reliable path from markdown files to a usable MCP docs server.
kind: guide
group: core
aliases:
  - quick-start
  - short-path
relatedUris:
  - workbench://install
  - workbench://guides/authoring
  - workbench://specs/public-surface
tags:
  - workflow
  - onboarding
priority: 110
includeInSearch: true
searchKind: guide
---

# Fast Path

If the docs already exist, the shortest path is:

1. Put the content in markdown files under `content/`.
2. Add front matter for `title`, `kind`, `group`, and the other metadata you
   want to surface.
3. Run `npm run build:mcp`.
4. Start `npm run dev:mcp` or deploy the Worker.

The server never reads from the docs tree at runtime. It serves the compiled
manifest and search index that were produced during the build.
