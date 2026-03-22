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
    - /specs/SPEC-QA-QUALITY-EVIDENCE.md
  adrs:
    - /decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  files:
    - /contracts/quality-evidence-model.md
    - /specs/SPEC-QA-QUALITY-EVIDENCE.md
    - /decisions/ADR-2026-03-07-quality-evidence-operating-model.md
  prs: []
  issues: []
  branches: []
title: add normalized test inventory discovery and schema
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /work/items/TASK-0016-add-normalized-test-inventory-discovery-and-schema.md
---

# TASK-0016 - add normalized test inventory discovery and schema

## Summary

Discover .NET test projects and test cases, then emit
`artifacts/quality/testing/test-inventory.json` using the proposed inventory
schema.

## Context

-

## Traceability

- Requirement IDs: []
- Architecture docs: []
- Verification docs: []
- Related contracts or runbooks: []

## Implementation notes

-

## Acceptance criteria

- Inventory output records discovered test projects, frameworks, and stable test
  identifiers.
- Discovery warnings are preserved in the artifact instead of silently dropping
  unknown tests.
- The output matches `schemas/test-inventory.schema.json`.

## Notes

-
