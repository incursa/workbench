---
artifact_id: SPEC-CLI-WORKTREE-START
artifact_type: specification
title: "CLI Worktree Start Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-WORKTREE
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-WORKTREE-START - CLI Worktree Start Command

## Purpose

Define the contract for creating or reusing a worktree and optionally launching
Codex.

## Scope

- `workbench worktree start`

## REQ-CLI-WORKTREE-START-0001 `workbench worktree start`

`worktree start` MUST accept the documented slug, ticket, base, root, prompt,
start-codex, and codex-terminal options, create or reuse the requested
worktree, and only launch Codex when explicitly requested.

## REQ-CLI-WORKTREE-START-0002 Branch and directory naming

`worktree start` MUST derive the branch and worktree directory names from the
slug and optional ticket in a deterministic way.

## REQ-CLI-WORKTREE-START-0003 Codex launch control

`worktree start` MUST not launch Codex unless `--start-codex` is set.

## REQ-CLI-WORKTREE-START-0004 Codex terminal mode

`worktree start` MUST honor `--codex-terminal` when it launches Codex.

## REQ-CLI-WORKTREE-START-0005 Worktree reuse

`worktree start` MUST reuse an existing matching worktree when the derived
path already exists.
