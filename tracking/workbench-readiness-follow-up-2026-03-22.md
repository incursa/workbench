# Workbench readiness follow-up

This note captures the concrete follow-up steps needed to move Workbench from
"mostly implemented" to "ready for real-world spec authoring, editing,
validation, and maintenance".

## Command surface

1. Rebuild and republish the shipped `workbench` tool so the installed command
   surface matches the checked-in source tree.
2. Regenerate `specs/generated/commands.md` from the live command tree and keep
   `workbench doc regen-help --check` green.
3. Keep the source help, installed tool help, and AI help surface aligned so
   `workbench spec`, `workbench validate`, and `workbench nav sync` are all
   discoverable from the same command tree.

## Validation And Spec Correctness

1. Change `workbench spec new` so a newly created specification starts from a
   parseable, policy-compliant starter body instead of a placeholder-only
   requirement skeleton.
2. Validate artifact IDs at write time, not only during later repo validation.
3. Keep requirement parsing, canonical trace labels, and one-keyword clause
   enforcement in sync with the spec template and the validator.

## Repo Scaffolding And Derived Views

1. Make scaffold/init produce a self-validating workspace, including the schema
   fixtures and config values that validation expects.
2. Confirm that the scaffolded spec and README files use the same front-matter
   conventions that the validator and doc schemas accept.
3. Keep `nav sync` and the derived navigation regeneration safe, repeatable,
   and dry-run
   friendly.

## Docs And AI Guidance

1. Update the checked-in command help snapshot and any stale doc references
   after the command surface is fixed.
2. Keep `workbench llm help` as the primary AI bootstrap surface.
3. Defer any separate `LLMS.txt` until the command tree is stable and there is a
   clear external need for another bootstrap file.

## Tests And Smoke Coverage

1. Add an end-to-end smoke test that boots a temp repo, seeds the required
   schema/config fixtures, creates a valid spec, validates it, and runs
   `nav sync`.
2. Fix the navigation and CLI-help contract tests so they assert the semantic
   roots and the live command tree rather than legacy paths or stale snapshots.
3. Run the full solution test suite green before calling the repo release-ready.

## Next Step

The first implementation task is to fix the bootstrap path for
`workbench spec new` so a fresh spec can be created as a valid, parseable draft
instead of a placeholder skeleton.
