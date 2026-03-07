---
id: TASK-0016
type: task
status: draft
priority: high
owner: platform
created: 2026-03-07
updated: null
githubSynced: null
tags:
  - quality
  - testing
  - phase-1
related:
  specs:
    - /docs/10-product/feature-spec-quality-evidence-testing-v1.md
  adrs:
    - /docs/40-decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /docs/30-contracts/quality-evidence-model.md
  prs: []
  issues: []
  branches: []
---

# TASK-0016 - Add normalized test inventory discovery and schema

## Summary

Discover .NET test projects and test cases, then emit
`artifacts/quality/testing/test-inventory.json` using the proposed inventory
schema.

## Acceptance criteria

- Inventory output records discovered test projects, frameworks, and stable test
  identifiers.
- Discovery warnings are preserved in the artifact instead of silently dropping
  unknown tests.
- The output matches `docs/30-contracts/test-inventory.schema.json`.
