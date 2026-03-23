---
artifact_id: SPEC-CLI-DOC-SUMMARIZE
artifact_type: specification
title: "CLI Doc Summarize Command"
domain: CLI
capability: command-surface
status: draft
owner: platform
related_artifacts:
  - SPEC-CLI-DOC
  - SPEC-CLI-SURFACE
  - WI-WB-0024
---

# SPEC-CLI-DOC-SUMMARIZE - CLI Doc Summarize Command

## Purpose

Define the contract for summarizing markdown diffs into doc-change notes.

## Scope

- `workbench doc summarize`

## REQ-CLI-DOC-SUMMARIZE-0001 `workbench doc summarize`

`doc summarize` MUST accept the documented staged/path/dry-run/update-index
options, summarize markdown diffs only, append a change note when AI is
available, and leave files unchanged in dry-run mode.

## REQ-CLI-DOC-SUMMARIZE-0002 Markdown scope

`doc summarize` MUST ignore non-markdown changes and summarize only the staged
or selected markdown files.

## REQ-CLI-DOC-SUMMARIZE-0003 Update-index behavior

`doc summarize` MUST stage the updated files when `--update-index` is set.

## REQ-CLI-DOC-SUMMARIZE-0004 Change-note scope

`doc summarize` MUST append no more than one change note per invocation.

## REQ-CLI-DOC-SUMMARIZE-0005 Failure behavior

`doc summarize` MUST leave files unchanged when the AI summary step fails.

## REQ-CLI-DOC-SUMMARIZE-0006 Context fidelity

`doc summarize` MUST keep the generated note tied to the selected markdown
changes.

## REQ-CLI-DOC-SUMMARIZE-0007 Empty-diff handling

`doc summarize` MUST make no changes when no markdown differences are
selected.
