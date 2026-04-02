# Workbench Docs MCP

Deterministic Cloudflare Worker MCP server for Workbench markdown docs.

The source of truth is static markdown under `content/**/*.md`. A build step
reads front matter from those files, compiles a manifest and search index into
`dist/mcp/*.json`, and bundles a tiny Worker that serves:

- `GET /mcp` for the human-readable docs index
- `POST /mcp` for MCP JSON-RPC traffic
- `GET /mcp/resource/<uri>` for browsable resource pages
- `workbench://file/{path}` as the markdown source-path template

The only dynamic tool in v1 is `search_docs`.

## Local Development

Run all commands from the repository root.

Typical loop:

```bash
npm install
npm run build:mcp
npm test
npm run dev:mcp
```

`npm run dev:mcp` starts the Worker locally through Wrangler. Open the URL it
prints and use:

- `GET /mcp` to browse the generated docs index
- `GET /mcp/resource/<uri>` to inspect a specific resource page
- `POST /mcp` from an MCP client

If the Worker is mounted behind a path prefix such as `/workbench`, the
prefixed forms `/<prefix>/mcp` and `/<prefix>/mcp/resource/<uri>` also work.

## Markdown Authoring

Each documentation file is a markdown file with front matter.

Example:

```md
---
uri: workbench://overview
slug: overview
title: Overview
summary: How Workbench organizes docs and the MCP surface.
kind: guide
group: core
aliases:
  - home
  - intro
relatedUris:
  - workbench://install
  - workbench://fast-path
priority: 120
---

# Overview

The body contains the actual documentation.
```

Supported front matter fields:

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

Build-time validation fails on duplicate URIs, duplicate slugs within a group,
unsupported kinds, and broken `relatedUris`.

## Build Pipeline

The build scripts do two things:

1. `scripts/generate-mcp.mjs` parses the content tree and writes the generated
   manifest and search index into `dist/mcp/`.
2. `scripts/build-mcp.mjs` regenerates the manifests and bundles the Worker.

Treat `dist/mcp/*` as generated output only. Do not edit those files by hand.

## Tests

```bash
npm test
```

The test suite rebuilds the Worker and checks:

- docs index rendering
- `initialize`
- resource listing
- resource template listing
- resource reads
- `tools/list`
- `tools/call` for `search_docs`
- search ranking and filtering

## Cloudflare Deployment

```bash
npm run deploy:mcp
```

The deployment workflow expects:

- `CLOUDFLARE_API_TOKEN`
- `CLOUDFLARE_ACCOUNT_ID`

The Worker defaults to `/workbench` as its mount prefix. If your load balancer
forwards a different path, set `MCP_PATH_PREFIX` in
[`wrangler.toml`](../wrangler.toml) and keep the route path aligned with that
prefix.

The Worker endpoint is usually:

```text
https://<your-worker-host>/<prefix>/mcp
```

## Project Layout

- `content/` - markdown source files with front matter
- `scripts/generate-mcp.mjs` - compiles markdown into `dist/mcp/*.json`
- `scripts/build-mcp.mjs` - regenerates manifests and bundles the Worker
- `src/mcp/worker.ts` - Cloudflare Worker MCP transport and docs UI
- `dist/mcp/` - generated manifest, search index, and Worker bundle
- `tests/mcp/` - protocol and search tests
