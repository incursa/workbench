---
artifact_id: SPEC-CLI-CONFIG-SHOW
artifact_type: specification
title: "CLI Config Show Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-CONFIG
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-CONFIG-SHOW - CLI Config Show Command

## Purpose

Define the contract for rendering the effective configuration.

## Scope

- `workbench config show`

## REQ-CLI-CONFIG-SHOW-0001 `workbench config show`

`config show` MUST print the effective configuration, including defaults, repo
config, and CLI overrides, and support machine-readable output.

## REQ-CLI-CONFIG-SHOW-0002 Read-only output

`config show` MUST be read-only and leaves config files, credentials files,
and derived artifacts unchanged.

## REQ-CLI-CONFIG-SHOW-0003 Effective state

`config show` MUST resolve values in the same precedence order used by the
runtime CLI so the displayed configuration matches actual command behavior.

## REQ-CLI-CONFIG-SHOW-0004 Output completeness

`config show` MUST include the resolved values for all documented settings in
the selected output format.

## REQ-CLI-CONFIG-SHOW-0005 Help alignment

`config show` MUST present the same setting names that the runtime CLI uses.

## REQ-CLI-CONFIG-SHOW-0006 Stable ordering

`config show` MUST keep the ordering of displayed settings stable for the
same repository state.
