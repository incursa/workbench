# Product coherence follow-up (2026-03-07)

This pass clarified the operating model, improved the work-item happy path, and
cleaned up the generated human views. The repo is more coherent, but several
ergonomic gaps remain.

## Not solved tonight

- The command surface still has overlapping entry points such as `sync`,
  `item sync`, `doc sync`, `nav sync`, and navigation regeneration, which takes time to
  learn even if each command is individually reasonable.
- Human help, agent help, and the checked-in CLI help doc still duplicate the
  same surface in different formats, so drift is still possible.
- Work items still duplicate some meaning between front matter and body
  headings/sections, which makes edits more fragile than they should be.
- Older product/spec docs still describe removed or superseded workflows such as
  legacy command names, deprecated flags, or aspirational TUI behavior.

## Larger redesign items

- A single-source help/export system that can generate `specs/generated/commands.md`
  from the live command tree.
- A narrower top-level command model that distinguishes local authoring,
  derived-view refresh, and GitHub sync more explicitly.
- A safer editing workflow for work item bodies so humans and agents do not have
  to manually keep title, summary, and acceptance criteria in sync.
- A clearer long-term stance on which GitHub sync behaviors should remain
  optional versus first-class.

## Prioritized next steps

1. Snapshot or generate the checked-in CLI help from the live command tree so
   the docs contract stops drifting from the executable.
2. Audit product/spec/skills docs for stale command names and obsolete path
   examples, then fix them in one pass.
3. Simplify the mental model around refresh commands by documenting and possibly
   consolidating the common local-only path versus GitHub-sync path.
4. Add a focused edit command or repair workflow for work-item body sections so
   canonical work items are easier to maintain without manual structure edits.
5. Revisit the TUI/onboarding specs after the local operating model is stable,
   so future UX work grows from the clarified repo-native model instead of
   competing with it.

## Related decisions

- `/architecture/ARC-WB-0004-repo-native-operating-model.md`
