---
uri: workbench://guides/search
slug: search
title: Search Guide
summary: How deterministic full-text search ranks the compiled Workbench docs.
kind: guide
group: guides
aliases:
  - find-docs
  - full-text-search
  - search-index
relatedUris:
  - workbench://overview
  - workbench://specs/public-surface
  - workbench://specs/verification-index
tags:
  - search
  - ranking
  - filters
priority: 98
includeInSearch: true
searchKind: guide
---

# Search Guide

`search_docs` is the only dynamic tool in v1. It searches the compiled docs
deterministically and ranks matches over:

- titles
- summaries
- aliases
- tags
- body text
- source paths
- URIs

The search tool also supports filtering by `kind` and `group`. That keeps the
result set narrow when you already know which part of the docs tree you want.

An exact title match should outrank a broad body-text match. If the query is
empty, the tool falls back to the docs' configured priorities.
