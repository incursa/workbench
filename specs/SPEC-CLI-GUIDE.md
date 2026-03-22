---
artifact_id: SPEC-CLI-GUIDE
artifact_type: specification
title: "CLI Guide Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-ONBOARDING
  - TASK-0001
---

# SPEC-CLI-GUIDE - CLI Guide Command

## Purpose

Define the contract for the interactive command discovery helper.

## Scope

- `workbench guide`

## REQ-CLI-GUIDE-0001 `workbench guide`

`guide` MUST run the interactive guide for common tasks, help users choose the
next repo-native action, and exit back to the normal command model with a short
next-step message.

## REQ-CLI-GUIDE-0002 Command routing

`guide` MUST route the user toward the live command tree and does not present
retired aliases or stubbed actions as available commands.

## REQ-CLI-GUIDE-0003 Read-only guidance

`guide` MUST remain read-only until the user explicitly selects a command that
performs a mutation.

## REQ-CLI-GUIDE-0002 Command discovery

`guide` MUST present the common repo actions in terms of the live command tree
and not refer to stale aliases or retired entry points.
