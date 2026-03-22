---
artifact_id: SPEC-CLI-PROMOTE
artifact_type: specification
title: "CLI Promote Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-OPERATIONS
  - TASK-0024
---

# SPEC-CLI-PROMOTE - CLI Promote Command

## Purpose

Define the contract for work-item promotion and branch scaffolding.

## Scope

- `workbench promote`

## REQ-CLI-PROMOTE-0001 `workbench promote`

`promote` MUST accept the documented type, title, push, start, PR, draft, and
base options, create the work item and branch scaffolding in one flow, and only
create a PR when explicitly requested.

## REQ-CLI-PROMOTE-0002 Branch workflow

`promote` MUST create the work item before branch or PR actions so the branch
and repository metadata always point at a real local item.

## REQ-CLI-PROMOTE-0003 PR creation controls

`promote` MUST only create or draft a PR when `--pr` is supplied and respects
the `--draft` and `--no-draft` intent explicitly.

## REQ-CLI-PROMOTE-0004 Failure short-circuit

`promote` MUST stop before branch or PR creation if work-item creation fails.

## REQ-CLI-PROMOTE-0005 Branch rollback

`promote` MUST leave the repository branch state unchanged if the branch setup
step fails.
