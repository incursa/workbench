---
artifact_id: SPEC-CLI-GITHUB
artifact_type: specification
title: "CLI GitHub Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-GITHUB-PR
  - SPEC-CLI-GITHUB-PR-CREATE
  - WI-WB-0024
---

# SPEC-CLI-GITHUB - CLI GitHub Commands Index

## Purpose

Define the navigation index for GitHub coordination commands.

## Scope

- `workbench github`

## REQ-CLI-GITHUB-0001 Index coverage

The `github` index MUST point at the dedicated leaf specs for each exposed
GitHub command and stay in sync with the live command tree.

## REQ-CLI-GITHUB-0002 Optional integration layer

The `github` group root MUST continue treating GitHub as an optional
coordination layer rather than the canonical source of truth.

## REQ-CLI-GITHUB-0003 Provider boundary

The `github` family MUST remain optional and surface provider failures through
leaf commands rather than implying that GitHub is required for every repo
workflow.

## REQ-CLI-GITHUB-0004 PR surface boundary

The `github` index MUST keep the documented pull-request surface explicit and
limited to the `pr` branch and its leaf command.

## REQ-CLI-GITHUB-0005 Child exposure

The `github` index MUST expose `pr` and `pr create` as its documented child
commands.

## Command Family Catalog

- [`SPEC-CLI-GITHUB-PR`](SPEC-CLI-GITHUB-PR.md)
- [`SPEC-CLI-GITHUB-PR-CREATE`](SPEC-CLI-GITHUB-PR-CREATE.md)
