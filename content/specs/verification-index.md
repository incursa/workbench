---
uri: workbench://specs/verification-index
slug: verification-index
title: Verification Index
summary: The checks that prove the docs index, transport, and search contract work together.
kind: spec
group: specs
aliases:
  - verification
  - test-matrix
  - checks
relatedUris:
  - workbench://specs/public-surface
  - workbench://update
tags:
  - tests
  - verification
  - build
priority: 88
includeInSearch: true
searchKind: spec
---

# Verification Index

Before shipping, verify:

- docs index rendering
- MCP `initialize`
- `resources/list`
- `resources/templates/list`
- `resources/read`
- `tools/list`
- `tools/call` for `search_docs`
- search ranking behavior
- search filtering behavior

The test suite should rebuild the Worker and exercise the compiled manifests,
not runtime source markdown.
