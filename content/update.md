---
uri: workbench://update
slug: update
title: Update
summary: Refresh the compiled docs after markdown changes and re-run the verification loop.
kind: guide
group: core
aliases:
  - refresh
  - rebuild
  - upgrade
relatedUris:
  - workbench://install
  - workbench://specs/verification-index
  - workbench://reference/layout
tags:
  - build
  - verification
priority: 108
includeInSearch: true
searchKind: guide
---

# Update

When the markdown changes, rebuild the docs server artifacts:

```bash
npm run build:mcp
npm test
```

`build:mcp` regenerates the manifests and bundles the Worker. `npm test`
rebuilds the artifacts again and checks the MCP transport, the docs index, and
the search behavior.

Treat `dist/mcp/` as generated output only. If you change content, change the
markdown source files and rerun the build.
