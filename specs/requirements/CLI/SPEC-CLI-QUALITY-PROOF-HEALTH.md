---
artifact_id: SPEC-CLI-QUALITY-PROOF-HEALTH
artifact_type: specification
title: "CLI Quality Proof Health Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-QUALITY
  - SPEC-CLI-SURFACE
  - SPEC-QA-QUALITY-EVIDENCE
  - WI-WB-0024
---

# SPEC-CLI-QUALITY-PROOF-HEALTH - CLI Quality Proof Health Command

## Purpose

Define the contract for classifying per-requirement proof health from authored
coverage expectations and discovered test evidence.

## Scope

- `workbench quality proof-health`

## REQ-CLI-QUALITY-PROOF-HEALTH-0001 Command options

`quality proof-health` MUST accept the documented contract, scope, gaps,
default-required, and output-format options without requiring normalized
quality artifacts to exist.

## REQ-CLI-QUALITY-PROOF-HEALTH-0002 Read-only behavior

`quality proof-health` MUST inspect repository content and discovered test
inventory without writing generated artifacts or mutating canonical trace.

## REQ-CLI-QUALITY-PROOF-HEALTH-0003 Coverage-contract classification

`quality proof-health` MUST classify every selected requirement into stable
proof-health states using authored coverage contracts, direct test refs, gap
ledger references, and discovered requirement test traits.

## REQ-CLI-QUALITY-PROOF-HEALTH-0004 Focused evidence distinction

`quality proof-health` MUST distinguish focused single-requirement requirement
home tests from broader linked tests when deciding whether proof is too broad.

## REQ-CLI-QUALITY-PROOF-HEALTH-0005 Scoped diagnostics

`quality proof-health` MUST constrain parsing and diagnostics to path-scoped
requirement selections when a repo-relative path scope is supplied.

## REQ-CLI-QUALITY-PROOF-HEALTH-0006 Machine-readable output

`quality proof-health` MUST support machine-readable output that includes
summary counts, per-requirement state, required evidence kinds, observed
evidence kinds, missing evidence kinds, coverage-contract source, direct test
refs, and warnings.
