---
workbench:
  type: runbook
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/50-runbooks/spec-cli-workflow.md"
  path: /docs/50-runbooks/spec-cli-workflow.md
owner: platform
status: active
updated: 2026-03-20
---

# Runbook: Spec CLI Workflow

## Purpose

Use the dedicated `workbench spec` command family to create, inspect, edit, link,
unlink, delete, and synchronize requirement specifications without falling back
to ad hoc Markdown editing.

## Scope

This runbook covers repository-native spec authoring in Workbench:

- `workbench spec new`
- `workbench spec show`
- `workbench spec edit`
- `workbench spec delete`
- `workbench spec link`
- `workbench spec unlink`
- `workbench spec sync`

It also explains when to use the compatibility `workbench doc` surface and how
policy-driven spec IDs work.

## Preconditions

- You are inside a Workbench repository.
- The repo has the canonical `specs/requirements` tree and a Workbench config file.
- If the repo defines `artifact-id-policy.json`, you know the domain and
  capability metadata that should be used for the spec ID pattern.

## Required access/tools

- The `workbench` CLI, preferably the repo-pinned tool or `workbench.ps1`.
- A text editor or the Workbench `Specs` page if you want form-based editing.
- Repository write access if you plan to create or modify files.

## Procedure

1. Inspect the current spec or the spec list.
   ```bash
   workbench spec show SPEC-CLI-ONBOARDING
   workbench spec show specs/requirements/CLI/SPEC-CLI-ONBOARDING.md
   ```
2. Create a new spec.
   ```bash
   workbench spec new --title "CLI onboarding and setup" --domain CLI --capability ONBOARDING
   ```
3. Override the artifact ID only when you need a specific value.
   ```bash
   workbench spec new --title "CLI onboarding and setup" --artifact-id SPEC-CLI-ONBOARDING
   ```
4. Keep the spec body structured.
   - Fill in `Purpose`, `Scope`, `Context`, `Requirements`, `Trace`, `Notes`, and `Open Questions` as needed.
   - Keep requirement blocks stable and numbered with 4-digit sequences.
   - When a field names another repository document, use a clickable Markdown
     link or a repo path that Workbench can resolve into one on save.
5. Link the spec to work items when delivery work starts.
   ```bash
   workbench spec link --path specs/requirements/CLI/SPEC-CLI-ONBOARDING.md --work-item TASK-0001
   ```
6. Update or remove traceability later if the scope changes.
   ```bash
   workbench spec edit SPEC-CLI-ONBOARDING --status approved
   workbench spec unlink --path specs/requirements/CLI/SPEC-CLI-ONBOARDING.md --work-item TASK-0001
   ```
7. Sync backlinks and front matter after larger edits or file moves.
   ```bash
   workbench spec sync --all
   workbench validate
   ```
8. Use the browser UI when you want a structured editor.
   - Open `Specs` in `workbench web`.
   - Leave `Artifact ID` blank if the repo policy should generate one.
   - Fill `Domain` and `Capability` when the repo uses customized spec IDs.

## Validation

- The spec file exists under `specs/requirements`.
- `workbench validate` passes with no broken links or schema errors.
- Related work items point back to the spec file or artifact ID.
- If a repo policy is active, the generated artifact ID matches the configured
  template.
- Specs, architecture docs, and ADRs should render clickable repository links
  wherever they refer to another local document.

## Related docs

- [Specification and traceability standard](/docs/00-overview/specification-and-traceability-standard.md)
- [Workbench repo spec](/docs/00-overview/workbench-spec.md)
- [Requirements README](/specs/requirements/README.md)
- [Requirement spec template](/docs/templates/requirement-spec.md)
