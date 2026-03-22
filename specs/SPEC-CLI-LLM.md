---
artifact_id: SPEC-CLI-LLM
artifact_type: specification
title: "CLI LLM Bootstrap Commands"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-SURFACE
  - TASK-0024
---

# SPEC-CLI-LLM - CLI LLM Bootstrap Commands

## Purpose

Define the contract for the AI bootstrap surface used by LLM agents.

## Scope

- `workbench llm`
- `workbench llm help`

## REQ-CLI-LLM-0001 `workbench llm`

The `llm` group MUST act as the AI bootstrap surface for agents and keep its
output human-readable and command-tree complete.

## REQ-CLI-LLM-0002 `workbench llm help`

`llm help` MUST print the comprehensive command reference for AI agents, keep
the command tree in one stream, and avoid introducing a separate `LLMS.txt`
surface unless the repository explicitly decides to add one later.

## REQ-CLI-LLM-0003 Bootstrap boundary

`llm` MUST remain read-only and not require repository mutation or external
configuration changes to display its help surface.

## REQ-CLI-LLM-0004 Tree parity

`llm help` MUST mirror the live CLI tree ordering and naming so agents can use
it as the authoritative entrypoint.

## REQ-CLI-LLM-0005 Family coverage

The `llm` help surface MUST include the documented top-level command families.

## REQ-CLI-LLM-0005 Read-only surface

The `llm` family MUST stay read-only and avoid repository mutation commands.
