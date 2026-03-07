---
id: TASK-0015
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

# TASK-0015 - Expand test gate into authored testing intent

## Summary

Evolve `docs/30-contracts/test-gate.contract.yaml` from a narrow threshold file
into the authored testing-intent contract for the quality evidence subsystem.

## Acceptance criteria

- The contract captures expected evidence kinds, confidence target, critical
  files, required tests, and intentional gaps.
- V1 remains compatible with the existing gate-style coverage fields.
- The authored contract shape is documented clearly enough that humans and AI
  agents can distinguish it from generated observed evidence.
