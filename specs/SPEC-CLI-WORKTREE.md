---
artifact_id: SPEC-CLI-WORKTREE
artifact_type: specification
title: "CLI Worktree Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-WORKTREE-START
  - TASK-0024
---

# SPEC-CLI-WORKTREE - CLI Worktree Commands Index

## Purpose

Define the navigation index for worktree bootstrap and Codex launch
coordination.

## Scope

- `workbench worktree`

## REQ-CLI-WORKTREE-0001 Index coverage

The `worktree` index MUST point at the dedicated leaf spec for the exposed
worktree start command and stay in sync with the live command tree.

## REQ-CLI-WORKTREE-0002 Branch isolation

The `worktree` group root MUST keep branch/worktree creation separate from the
rest of the repo sync surface.

## REQ-CLI-WORKTREE-0003 Workspace scope

`worktree start` MUST create or reuse a task worktree and keep its changes
scoped to workspace bootstrap.

## REQ-CLI-WORKTREE-0004 Sync boundary

The `worktree` family MUST not perform repo sync or work-item mutation beyond
the `start` flow.

## Command Family Catalog

- [SPEC-CLI-WORKTREE-START](./SPEC-CLI-WORKTREE-START.md)
