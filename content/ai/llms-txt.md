---
uri: workbench://ai/llms-txt
slug: llms-txt
title: AI Bootstrap
summary: A concise bootstrap for agents that need the Workbench docs surface.
kind: guide
group: ai
aliases:
  - llms
  - bootstrap
  - agent-instructions
relatedUris:
  - workbench://overview
  - workbench://guides/authoring
  - workbench://guides/search
tags:
  - ai
  - bootstrap
  - agents
priority: 80
includeInSearch: true
searchKind: guide
---

# AI Bootstrap

Use this repository in this order:

1. Read the docs index at `/mcp`.
2. Open `overview` for the high-level model.
3. Read the authoring and search guides before editing docs.
4. Treat `dist/mcp/` as generated output only.
5. Use `search_docs` when you need a deterministic query over the compiled
   content.

Keep the runtime simple:

- no LLM calls
- no runtime crawling
- no database
- no dynamic web search
- one search tool only
