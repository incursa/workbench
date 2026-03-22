---
artifact_id: SPEC-CLI-SURFACE
artifact_type: specification
title: "CLI Surface and Command Contract Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-BOARD
  - SPEC-CLI-BOARD-REGEN
  - SPEC-CLI-CODEX
  - SPEC-CLI-CODEX-DOCTOR
  - SPEC-CLI-CODEX-RUN
  - SPEC-CLI-CONFIG
  - SPEC-CLI-CONFIG-CREDENTIALS
  - SPEC-CLI-CONFIG-CREDENTIALS-SET
  - SPEC-CLI-CONFIG-CREDENTIALS-UNSET
  - SPEC-CLI-CONFIG-SET
  - SPEC-CLI-CONFIG-SHOW
  - SPEC-CLI-DOC
  - SPEC-CLI-DOC-DELETE
  - SPEC-CLI-DOC-EDIT
  - SPEC-CLI-DOC-LINK
  - SPEC-CLI-DOC-NEW
  - SPEC-CLI-DOC-REGEN-HELP
  - SPEC-CLI-DOC-SHOW
  - SPEC-CLI-DOC-SUMMARIZE
  - SPEC-CLI-DOC-SYNC
  - SPEC-CLI-DOC-UNLINK
  - SPEC-CLI-DOCTOR
  - SPEC-CLI-GITHUB
  - SPEC-CLI-GITHUB-PR
  - SPEC-CLI-GITHUB-PR-CREATE
  - SPEC-CLI-GUIDE
  - SPEC-CLI-ITEM
  - SPEC-CLI-INIT
  - SPEC-CLI-LLM
  - SPEC-CLI-MIGRATE
  - SPEC-CLI-NAV
  - SPEC-CLI-NAV-SYNC
  - SPEC-CLI-OPERATIONS
  - SPEC-CLI-NORMALIZE
  - SPEC-CLI-PROMOTE
  - SPEC-CLI-QUALITY
  - SPEC-CLI-QUALITY-SHOW
  - SPEC-CLI-QUALITY-SYNC
  - SPEC-CLI-SCAFFOLD
  - SPEC-CLI-SPEC
  - SPEC-CLI-SPEC-DELETE
  - SPEC-CLI-SPEC-EDIT
  - SPEC-CLI-SPEC-LINK
  - SPEC-CLI-SPEC-NEW
  - SPEC-CLI-SPEC-SHOW
  - SPEC-CLI-SPEC-SYNC
  - SPEC-CLI-SPEC-UNLINK
  - SPEC-CLI-SYNC
  - SPEC-CLI-VALIDATE
  - SPEC-CLI-VERSION
  - SPEC-CLI-VOICE
  - SPEC-CLI-VOICE-DOC
  - SPEC-CLI-VOICE-WORKITEM
  - SPEC-CLI-WORKTREE
  - SPEC-CLI-WORKTREE-START
  - SPEC-CLI-ONBOARDING
  - TASK-0024
---

# SPEC-CLI-SURFACE - CLI Surface and Command Contract Index

## Purpose

Define the root CLI contract and provide the authoritative navigation map to
the focused command-family specifications.

## Scope

- command discovery and root help behavior
- global option normalization
- exit-code and output conventions
- the live command tree surfaced by `workbench --help` and
  `contracts/commands.md`

## Context

Workbench is a repo-native CLI first. The checked-in help snapshot is the
stable appendix for parameters and command discovery, while the family specs
hold the command-by-command behavioral requirements.

## Global Contract

The root CLI MUST accept the documented global options, normalize `--format`
and `--repo` consistently, and preserve the standard exit-code contract:
`0` success, `1` success with warnings, `2` failure.

## REQ-CLI-SURFACE-0001 Root command behavior

`workbench` MUST expose the documented top-level command families, keep help
output synchronized with the live command tree, and route command-specific
behavior to the dedicated family specs.

## REQ-CLI-SURFACE-0002 Help snapshot parity

The CLI help output and `contracts/commands.md` MUST stay synchronized with the
live command tree so automation can treat the snapshot as a reliable appendix.

## REQ-CLI-SURFACE-0003 Family-spec authority

Each focused family spec MUST remain the source of truth for that command tree
branch, including accepted parameters, output behavior, mutation rules, and
dry-run semantics.

## REQ-CLI-SURFACE-0004 Global option contract

`workbench` MUST accept the documented global options and normalize them
consistently across the command tree.

## REQ-CLI-SURFACE-0005 Exit-code contract

`workbench` MUST preserve the standard exit codes `0`, `1`, and `2` for
success, success with warnings, and failure.

## Command Family Catalog

- [SPEC-CLI-BOARD](./SPEC-CLI-BOARD.md)
- [SPEC-CLI-BOARD-REGEN](./SPEC-CLI-BOARD-REGEN.md)
- [SPEC-CLI-CODEX](./SPEC-CLI-CODEX.md)
- [SPEC-CLI-CODEX-DOCTOR](./SPEC-CLI-CODEX-DOCTOR.md)
- [SPEC-CLI-CODEX-RUN](./SPEC-CLI-CODEX-RUN.md)
- [SPEC-CLI-CONFIG](./SPEC-CLI-CONFIG.md)
- [SPEC-CLI-CONFIG-CREDENTIALS](./SPEC-CLI-CONFIG-CREDENTIALS.md)
- [SPEC-CLI-CONFIG-CREDENTIALS-SET](./SPEC-CLI-CONFIG-CREDENTIALS-SET.md)
- [SPEC-CLI-CONFIG-CREDENTIALS-UNSET](./SPEC-CLI-CONFIG-CREDENTIALS-UNSET.md)
- [SPEC-CLI-CONFIG-SET](./SPEC-CLI-CONFIG-SET.md)
- [SPEC-CLI-CONFIG-SHOW](./SPEC-CLI-CONFIG-SHOW.md)
- [SPEC-CLI-DOC](./SPEC-CLI-DOC.md)
- [SPEC-CLI-DOC-DELETE](./SPEC-CLI-DOC-DELETE.md)
- [SPEC-CLI-DOC-EDIT](./SPEC-CLI-DOC-EDIT.md)
- [SPEC-CLI-DOC-LINK](./SPEC-CLI-DOC-LINK.md)
- [SPEC-CLI-DOC-NEW](./SPEC-CLI-DOC-NEW.md)
- [SPEC-CLI-DOC-REGEN-HELP](./SPEC-CLI-DOC-REGEN-HELP.md)
- [SPEC-CLI-DOC-SHOW](./SPEC-CLI-DOC-SHOW.md)
- [SPEC-CLI-DOC-SUMMARIZE](./SPEC-CLI-DOC-SUMMARIZE.md)
- [SPEC-CLI-DOC-SYNC](./SPEC-CLI-DOC-SYNC.md)
- [SPEC-CLI-DOC-UNLINK](./SPEC-CLI-DOC-UNLINK.md)
- [SPEC-CLI-DOCTOR](./SPEC-CLI-DOCTOR.md)
- [SPEC-CLI-GITHUB](./SPEC-CLI-GITHUB.md)
- [SPEC-CLI-GITHUB-PR](./SPEC-CLI-GITHUB-PR.md)
- [SPEC-CLI-GITHUB-PR-CREATE](./SPEC-CLI-GITHUB-PR-CREATE.md)
- [SPEC-CLI-GUIDE](./SPEC-CLI-GUIDE.md)
- [SPEC-CLI-LLM](./SPEC-CLI-LLM.md)
- [SPEC-CLI-INIT](./SPEC-CLI-INIT.md)
- [SPEC-CLI-MIGRATE](./SPEC-CLI-MIGRATE.md)
- [SPEC-CLI-NAV](./SPEC-CLI-NAV.md)
- [SPEC-CLI-NAV-SYNC](./SPEC-CLI-NAV-SYNC.md)
- [SPEC-CLI-OPERATIONS](./SPEC-CLI-OPERATIONS.md)
- [SPEC-CLI-NORMALIZE](./SPEC-CLI-NORMALIZE.md)
- [SPEC-CLI-PROMOTE](./SPEC-CLI-PROMOTE.md)
- [SPEC-CLI-QUALITY](./SPEC-CLI-QUALITY.md)
- [SPEC-CLI-QUALITY-SHOW](./SPEC-CLI-QUALITY-SHOW.md)
- [SPEC-CLI-QUALITY-SYNC](./SPEC-CLI-QUALITY-SYNC.md)
- [SPEC-CLI-SCAFFOLD](./SPEC-CLI-SCAFFOLD.md)
- [SPEC-CLI-SPEC](./SPEC-CLI-SPEC.md)
- [SPEC-CLI-SPEC-DELETE](./SPEC-CLI-SPEC-DELETE.md)
- [SPEC-CLI-SPEC-EDIT](./SPEC-CLI-SPEC-EDIT.md)
- [SPEC-CLI-SPEC-LINK](./SPEC-CLI-SPEC-LINK.md)
- [SPEC-CLI-SPEC-NEW](./SPEC-CLI-SPEC-NEW.md)
- [SPEC-CLI-SPEC-SHOW](./SPEC-CLI-SPEC-SHOW.md)
- [SPEC-CLI-SPEC-SYNC](./SPEC-CLI-SPEC-SYNC.md)
- [SPEC-CLI-SPEC-UNLINK](./SPEC-CLI-SPEC-UNLINK.md)
- [SPEC-CLI-SYNC](./SPEC-CLI-SYNC.md)
- [SPEC-CLI-VALIDATE](./SPEC-CLI-VALIDATE.md)
- [SPEC-CLI-VERSION](./SPEC-CLI-VERSION.md)
- [SPEC-CLI-VOICE](./SPEC-CLI-VOICE.md)
- [SPEC-CLI-VOICE-DOC](./SPEC-CLI-VOICE-DOC.md)
- [SPEC-CLI-VOICE-WORKITEM](./SPEC-CLI-VOICE-WORKITEM.md)
- [SPEC-CLI-WORKTREE](./SPEC-CLI-WORKTREE.md)
- [SPEC-CLI-WORKTREE-START](./SPEC-CLI-WORKTREE-START.md)

## Notes

- `SPEC-CLI-ONBOARDING` remains the dedicated spec for first-run setup and the
  `guide` entrypoint.
- `TASK-0024` tracks the broader command-spec authoring work.
