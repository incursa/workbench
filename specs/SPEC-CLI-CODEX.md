---
artifact_id: SPEC-CLI-CODEX
artifact_type: specification
title: "CLI Codex Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-CODEX-DOCTOR
  - SPEC-CLI-CODEX-RUN
  - TASK-0024
---

# SPEC-CLI-CODEX - CLI Codex Commands Index

## Purpose

Define the navigation index for Codex-oriented CLI commands.

## Scope

- `workbench codex`

## REQ-CLI-CODEX-0001 Index coverage

The `codex` index MUST point at the dedicated leaf specs for each exposed
Codex command and stay in sync with the live command tree.

## REQ-CLI-CODEX-0002 Group-root behavior

The `codex` group root MUST remain non-mutating and keeps Codex-related
automation separate from core repository mutation commands.

## REQ-CLI-CODEX-0003 Coordination surface

The `codex` group root MUST remain a coordination surface that exposes the
documented AI entrypoints without changing repository content itself.

## REQ-CLI-CODEX-0004 Command-tree parity

The `codex` index MUST keep the documented `doctor` and `run` leaves visible
and aligned with the live command tree.

## REQ-CLI-CODEX-0005 Child exposure

The `codex` index MUST expose `doctor` and `run` as its documented child
commands.

## Command Family Catalog

- [SPEC-CLI-CODEX-DOCTOR](./SPEC-CLI-CODEX-DOCTOR.md)
- [SPEC-CLI-CODEX-RUN](./SPEC-CLI-CODEX-RUN.md)
