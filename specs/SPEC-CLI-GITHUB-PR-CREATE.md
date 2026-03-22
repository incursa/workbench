---
artifact_id: SPEC-CLI-GITHUB-PR-CREATE
artifact_type: specification
title: "CLI GitHub PR Create Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-GITHUB
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-GITHUB-PR-CREATE - CLI GitHub PR Create Command

## Purpose

Define the contract for creating pull requests from work items.

## Scope

- `workbench github pr create`

## REQ-CLI-GITHUB-PR-CREATE-0001 `workbench github pr create`

`github pr create` MUST accept the documented work-item ID and PR options,
create a pull request through the configured provider, and backlink the PR URL
onto the work item.

## REQ-CLI-GITHUB-PR-CREATE-0002 PR option handling

`github pr create` MUST honor `--base`, `--draft`, and `--fill` exactly as
documented and rejects invalid work-item references before calling the
provider.

## REQ-CLI-GITHUB-PR-CREATE-0003 Traceability boundary

`github pr create` MUST record the created PR URL back onto the originating
work item after a successful provider call.

## REQ-CLI-GITHUB-PR-CREATE-0004 Failure handling

`github pr create` MUST surface provider or authentication failures without
leaving the work item in a partially linked state.

## REQ-CLI-GITHUB-PR-CREATE-0005 Provider availability

`github pr create` MUST fail before updating backlinks when the configured
provider cannot be reached or authenticated.
