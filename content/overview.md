---
uri: workbench://overview
slug: overview
title: Overview
summary: How the Workbench docs MCP server turns markdown into a deterministic browsable docs surface.
kind: guide
group: core
aliases:
  - home
  - intro
relatedUris:
  - workbench://install
  - workbench://fast-path
  - workbench://guides/authoring
tags:
  - onboarding
  - mcp
priority: 120
includeInSearch: true
searchKind: guide
---

# Overview

Workbench Docs MCP is a markdown-first Cloudflare Worker that turns static
repository docs into MCP resources.

The source of truth is the `content/` tree. The build step parses front matter,
validates it, and emits deterministic manifests under `dist/mcp/`. The Worker
then serves:

- `GET /mcp` for the browsable HTML docs index
- `POST /mcp` for MCP JSON-RPC traffic
- `GET /mcp/resource/<uri>` for browsable resource pages

The runtime stays deterministic:

- no LLM calls
- no database
- no runtime crawling
- no dynamic web search
- one tool only: `search_docs`

Use this page as the entry point for the rest of the docs surface.
