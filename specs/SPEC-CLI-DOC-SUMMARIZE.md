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
  - TASK-0024
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
