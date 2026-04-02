---
uri: workbench://ai/agent-instructions
slug: agent-instructions
title: Agent instructions
summary: Repository-local operating notes for automated agents working on Workbench.
kind: guide
group: ai
aliases:
  - agent-guide
  - codex-bootstrap
relatedUris:
  - workbench://overview
  - workbench://guides/authoring
  - workbench://guides/search
  - workbench://reference/layout
priority: 55
includeInSearch: true
searchKind: guide
tags:
  - ai
  - agents
  - workflow
---

# Agent instructions

Treat the `content/` tree as the only authored source for the MCP docs server.
Do not mutate `dist/mcp/` by hand.

Prefer deterministic changes:

- update markdown source
- regenerate the manifest and search index
- run the MCP tests
- keep the URI namespace stable

For longer work, inspect the docs README first so you understand the local
development and deployment flow before changing the content tree.
