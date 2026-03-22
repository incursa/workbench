---
artifact_id: SPEC-CLI-GITHUB-PR
artifact_type: specification
title: "CLI GitHub PR Group"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-GITHUB
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-GITHUB-PR - CLI GitHub PR Group

## Purpose

Define the contract for the pull-request grouping command.

## Scope

- `workbench github pr`

## REQ-CLI-GITHUB-PR-0001 `workbench github pr`

`github pr` MUST act as a pure grouping command for pull-request operations and
must not mutate repository state by itself.

## REQ-CLI-GITHUB-PR-0002 Command tree alignment

`github pr` MUST keep its subcommand listing aligned with the live command
tree, even when the provider implementation changes.

## REQ-CLI-GITHUB-PR-0003 Child exposure

`github pr` MUST expose `create` as its only documented child command.

## REQ-CLI-GITHUB-PR-0004 Non-mutating root

`github pr` MUST not create or update pull requests directly.

## REQ-CLI-GITHUB-PR-0005 Help-tree parity

`github pr` MUST keep its help output aligned with the live provider-backed
PR command tree.
