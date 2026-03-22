---
artifact_id: SPEC-CLI-NAV
artifact_type: specification
title: "CLI Navigation Commands Index"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - SPEC-CLI-NAV-SYNC
  - TASK-0024
---

# SPEC-CLI-NAV - CLI Navigation Commands Index

## Purpose

Define the navigation index for derived navigation and workboard
synchronization commands.

## Scope

- `workbench nav`

## REQ-CLI-NAV-0001 Index coverage

The `nav` index MUST point at the dedicated leaf spec for the exposed
navigation sync command and stay in sync with the live command tree.

## REQ-CLI-NAV-0002 Derived-output boundary

The `nav` group root MUST only drive derived views and leaves canonical
authored content unchanged.

## REQ-CLI-NAV-0003 Derived-output scope

`nav sync` MUST regenerate indexes and the workboard as derived outputs rather
than editing authored content directly.

## REQ-CLI-NAV-0004 Link-order boundary

`nav sync` MUST reconcile links before rebuilding derived views when link
corrections are needed.

## Command Family Catalog

- [SPEC-CLI-NAV-SYNC](./SPEC-CLI-NAV-SYNC.md)
