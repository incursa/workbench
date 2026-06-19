---
title: "Verification Index"
---

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

This page lists the minimum checks that prove the compiled docs surface still
matches the markdown source tree.

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

These checks do not prove the mirrored docs PR in `incursa-docs`, the live
Cloudflare deployment, or any repo outside this source tree.
