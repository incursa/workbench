---
artifact_id: "SPEC-<DOMAIN>[-<GROUPING>...]"
artifact_type: specification
title: "<Specification Title>"
domain: "<domain>"
capability: "<capability-or-concern>"
status: draft
owner: "<team-or-role>"
tags:
  - "<tag>"
related_artifacts:
  - "<artifact-id>"
---

# SPEC-<DOMAIN>[-<GROUPING>...] - <Specification Title>

## Purpose

State the purpose of the specification in one or two direct paragraphs.

## Scope

Optional. State what is in scope and, if useful, what is out of scope.

## Context

Optional. Capture the business or technical context shared by the grouped requirements.

## REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> <Requirement Title>
The system MUST <direct, testable behavior>.

Trace:
- Satisfied By:
  - ARC-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
- Implemented By:
  - WI-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
- Verified By:
  - VER-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+>
- Test Refs:
  - <test reference>
- Code Refs:
  - <code reference>
- Related:
  - <artifact or requirement ID>

Notes:
- Optional clarification that narrows interpretation without changing the requirement.

## REQ-<DOMAIN>[-<GROUPING>...]-<SEQUENCE:4+> <Requirement Title>
The system SHOULD <recommended behavior>.

## Open Questions

- <question>

## Authoring Rules

- Every requirement clause must contain exactly one approved normative keyword in all caps: `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`, or `MAY`.
- The keyword does not need to be the first word, but it must appear in the clause and it must be the only approved keyword in that clause.
- The clause should express one obligation, rule, or constraint and should usually be a single sentence.
- Each specification Markdown file contains one specification and one or more related requirement clauses.
- `Trace` and `Notes` are optional.
- The clause is the normative content. Do not bury it under a required metadata block.
- If you add richer local metadata, keep it clearly optional and do not place it between the requirement heading and the clause.
- Front matter describes the document as a whole, not individual requirements.
- `Test Refs` and `Code Refs` stay implementation-specific. The standard does not prescribe a framework or comment syntax.
