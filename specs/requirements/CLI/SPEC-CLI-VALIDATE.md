---
artifact_id: SPEC-CLI-VALIDATE
artifact_type: specification
title: "CLI Validate Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - WI-WB-0024
---

# SPEC-CLI-VALIDATE - CLI Validate Command

## Purpose

Define the contract for repository validation.

## Scope

- `workbench validate`

## REQ-CLI-VALIDATE-0001 `workbench validate`

`validate` MUST accept the documented strict, verbose, include/exclude, and
skip-doc-schema options, validate work items and schemas, and distinguish
warnings from errors in the exit code.

## REQ-CLI-VALIDATE-0002 Warning semantics

`validate` MUST return a warning exit code only when non-fatal issues are found
and `--strict` is not set.

## REQ-CLI-VALIDATE-0003 Scope filters

`validate` MUST honor the link include and link exclude filters when checking
repository links.

## REQ-CLI-VALIDATE-0004 Error reporting

`validate` MUST report each validation failure with enough context for the
caller to locate the problem.

## REQ-CLI-VALIDATE-0005 Read-only behavior

`validate` MUST not write repository files or generated artifacts.

## REQ-CLI-VALIDATE-0006 Machine-readable output

`validate` MUST support machine-readable output when requested.
