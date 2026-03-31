# Runbook: CUE SpecTrace Repository Migration

## Purpose

Use this runbook when migrating another repository from Markdown, YAML, or JSON
authored SpecTrace artifacts to canonical CUE, using the updated `workbench`
tooling.

This document is intentionally conservative. The goal is to preserve existing
requirement meaning, IDs, and traceability while changing the canonical
authoring format and the repository workflow around it.

## Use This When

- The target repository already uses SpecTrace-style artifacts and must move to
  canonical CUE authoring.
- The latest `workbench` tool is already installed in the target repository
  environment.
- You want an agent or engineer to perform a full migration, not just produce a
  proposal.

## Preconditions

- Work from a dedicated git branch.
- Start from a clean working tree unless the migration explicitly includes
  pre-existing local changes.
- Confirm the repository owner wants canonical CUE, not dual-authoring.
- Confirm the latest `workbench` tool is available on `PATH`.
- Have access to the reference implementations:
  - the canonical standard repository
  - a repository already updated to the new model, when available

## Non-Negotiable Safeguards

- Preserve existing artifact IDs unless there is a real collision or invalid ID
  that must be corrected.
- Preserve requirement meaning. Normalize field names and structure, not the
  normative substance.
- Do not silently drop trace edges, notes, rationale, scope, or source
  citations.
- Treat Markdown as generated or presentation-only once canonical CUE is in
  place.
- Do not leave the repository in a mixed-policy state where Markdown remains an
  equally authoritative authoring surface.
- Do not mutate requirement files from derived evidence or test discovery.
- Make broken references, duplicate IDs, wrong target kinds, and missing nested
  refs fail validation.
- Update CI, docs, templates, and commands in the same change.

## Recommended Procedure

1. Capture the baseline.
   - `git status --short`
   - `workbench validate --profile core`
   - Run any repo-local validation or report-generation commands already used by
     the repository.
2. Inspect the current repository model.
   - Identify every authored SpecTrace family in use.
   - Identify how IDs are formatted today.
   - Identify cross-file and nested-item references.
   - Identify generated outputs and CI hooks.
3. Introduce canonical CUE support.
   - Add a root CUE module.
   - Add shared importable CUE packages for artifact schemas.
   - Add or update a local `Resolve-Cue.ps1` bootstrap if the repo does not
     already have a pinned CUE bootstrap.
4. Migrate authored artifacts.
   - Convert canonical authored artifacts to CUE.
   - Keep generated Markdown only as a derived surface.
   - Preserve prose and trace semantics.
5. Implement repository-wide validation.
   - Enforce schema shape, required fields, ID patterns, duplicate ID checks,
     and cross-file reference resolution.
   - Add a deterministic catalog or index when pure CUE is not enough for
     repository-wide foreign-key checks.
6. Update the workflow.
   - Update repo scripts, docs, templates, editor guidance, and CI.
   - Ensure `workbench validate`, navigation/report generation, and any quality
     or attestation steps work from canonical CUE inputs.
7. Prove the migration.
   - Run the repo validation commands.
   - Run the test suite.
   - Generate the human-readable outputs.
   - Summarize what was migrated and what remains, if anything.

## Copy/Paste Migration Prompt

Use the prompt below with an agent working inside the target repository.

```text
You are working inside an existing repository that already uses SpecTrace-style
artifacts. Update this repository so that canonical authored SpecTrace artifacts
are written in CUE and the repository workflow is aligned with the updated
Workbench tool.

Assumptions
- The latest `workbench` tool is already installed and available in this repo.
- You are allowed to make repository changes, not just propose them.
- Preserve existing repository intent, IDs, and traceability unless a concrete
  validation problem requires a correction.

Primary goal
- Convert the repository to canonical CUE-authored SpecTrace artifacts.
- Keep Markdown as generated or presentation-only output, not the source of
  truth.
- Update validation, generation, CI, docs, and templates so contributors can
  safely keep using the repository after the migration.

Mandatory execution rules
- First inspect the repository and identify the real artifact families, field
  names, templates, scripts, report generators, and CI workflows in use.
- Preserve existing requirement meaning and stable IDs wherever possible.
- Do not silently drop prose, notes, rationale, source refs, or trace fields.
- Do not leave the repository in a mixed-authority state where Markdown is still
  canonical.
- Do not mutate authored requirement trace from derived evidence, test
  discovery, or report generation.
- If there are cross-file or nested-item references, make them validate like
  foreign keys:
  - fail on missing targets
  - fail on duplicate IDs
  - fail on wrong target kind
  - fail when nested referenced items are removed
- Keep the solution understandable. Prefer straightforward data-oriented CUE
  over clever metaprogramming.

What to implement
1. Add a proper CUE module at the repository root.
   - Use a real module path if it can be determined from the repository.
   - Add shared importable packages for canonical artifact definitions.

2. Define canonical CUE schemas for the artifact families that actually exist in
   this repository.
   - Cover document-level metadata.
   - Cover narrative sections such as purpose, scope, context, summary, notes,
     rationale, and similar fields used by the repo.
   - Cover repeated structured items such as requirements, work items,
     verification entries, architecture decisions, evidence blocks, and trace
     blocks.
   - Make required vs optional fields explicit.
   - Add regex validation for IDs and enums where appropriate.
   - Normalize naming only when necessary and preserve meaning.

3. Formalize the repository's requirement model in CUE.
   - Requirements or requirement-like records must be first-class structured
     data, not loose Markdown parsing.
   - Preserve IDs, titles, statements, notes, source refs, and downstream trace.

4. Implement real cross-file reference validation.
   - Prefer stable IDs over file paths.
   - Distinguish artifact-level refs from nested-item refs.
   - Add a deterministic repository catalog or index if needed to support
     validation cleanly.

5. Keep human-readable outputs.
   - Preserve or introduce deterministic Markdown generation from canonical CUE.
   - If the repository has report pages, summary pages, evidence validation, or
     attestation output, update those workflows to operate from canonical CUE
     and the current evidence format.

6. Update repository tooling and automation.
   - Update scripts, validators, generators, report builders, templates, docs,
     and CI.
   - Use the updated `workbench` behavior where relevant.
   - Remove or retire obsolete flows that assumed Markdown was canonical or that
     sync derived test refs back into authored requirements.

7. Migrate the corpus.
   - Perform a best-effort full migration.
   - If a full migration is not realistic in one change, migrate a real
     end-to-end slice across every major artifact family and provide a
     deterministic migration path for the rest.
   - Do not leave the new model theoretical only.

8. Add or update validation coverage.
   - Include tests or fixtures for:
     - valid documents
     - missing required fields
     - invalid IDs
     - duplicate IDs
     - broken cross-file references
     - wrong target-kind references

Repository-specific expectations
- Reuse the repository's native language and tooling where sensible.
- If there is already a report or attestation pipeline, adapt it rather than
  replacing it blindly.
- If there is already a pinned CUE bootstrap pattern, keep it aligned with the
  repository's conventions.
- If Markdown is still committed for readability, make it clearly generated or
  read-only for canonical artifacts.

Required validation before finishing
- Run `workbench validate --profile core` at minimum.
- Run stronger validation profiles if the repository supports them.
- Run the repository test suite.
- Run any repo-local Markdown generation, evidence validation, or attestation
  commands needed to prove the new workflow works.
- If validation cannot be made fully green, report the remaining failures
  precisely and explain whether they are repository-content debt or a migration
  defect.

Required final report
- Summarize what changed.
- List the new canonical layout.
- List the validation and generation commands.
- Explain how cross-file references work now.
- State what was migrated and what remains.
- Call out any follow-up items explicitly.
```

## Expected Deliverables From The Migration

- A root CUE module.
- Shared CUE schema packages.
- Canonical CUE-authored SpecTrace artifacts or a real migration slice plus
  deterministic migration tooling.
- Deterministic generated Markdown or equivalent human-readable outputs.
- Updated repository validation and CI.
- Updated templates and authoring docs.
- A clear statement in the repo that CUE is canonical.

## Validation Checklist

Use this after the migration is applied:

- `workbench validate --profile core`
- `workbench validate --profile traceable` if the repository is expected to
  satisfy downstream trace completeness
- repo-local test suite
- repo-local Markdown generation or report-generation commands
- repo-local evidence validation and attestation commands, if present
- spot-check a few generated Markdown files against their canonical CUE sources
- inspect `git diff --stat` and confirm there are no unintended deletions of
  requirement prose or trace metadata

## Review Checklist

- Are requirement IDs unchanged?
- Are artifact IDs unchanged unless there was a necessary correction?
- Are all former trace relationships still represented in canonical structured
  fields?
- Are generated outputs deterministic?
- Is Markdown clearly non-canonical?
- Does CI fail when IDs, refs, or required fields are broken?
- Does the repo still expose a straightforward authoring path for contributors?

## Related Docs

- [`README.md`](../README.md)
- [`authoring.md`](../authoring.md)
- [`layout.md`](../layout.md)
- [`runbooks/cross-repo-migration-and-rollback.md`](./cross-repo-migration-and-rollback.md)
