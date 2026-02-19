---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/00-overview/workbench-public-release.md"
  path: /docs/00-overview/workbench-public-release.md
owner: platform
status: draft
updated: 2026-01-01
---

# Workbench, the calm command line for work that actually ships

There are plenty of tools that track work. There are far fewer tools that make
work feel inevitable—where the plan, the work, and the proof all live together,
side-by-side, inside the repo. That is the promise of Workbench: a .NET CLI that
turns your repository into a durable, navigable system of record, without the
weight of a web app or a fragile workflow.

Workbench is for teams who want *clarity over ceremony*. It is for small teams,
solo builders, and agent-driven workflows where every decision needs to live in
source control. It keeps the human-friendly Markdown you already write, while
quietly adding the structure and automation that make it scalable.

## The values that shape Workbench

**Work should be transparent.** If the real plan lives in a tool no one opens,
then it is not a plan. Workbench puts specs, decisions, and tasks in the repo so
anyone can see the state of work with a `git pull` and a single CLI command.

**Work should be durable.** Markdown files survive tooling migrations, API
changes, and vendor decisions. Workbench makes “work as code” first-class.

**Work should be linkable.** Specs should connect to tasks. Tasks should connect
to branches and PRs. Workbench turns those connections into a consistent, machine
readable web inside your repo.

**Work should be automatable.** The CLI emits predictable output, validates
schemas, and keeps templates stable so automation can be reliable and safe.

**Work should stay lightweight.** A single CLI, no server required, no opinion
about your stack. You decide how much ceremony you want.

## What you can do today

Workbench already brings a full set of primitives for disciplined execution:

- **Create work items as Markdown.** Each task/bug/spike is a file with YAML
  front matter that is both human readable and machine validated.
- **Promote work with confidence.** Workbench can create branches and commits
  that keep work items and implementation tied together from day one.
- **Standardize documentation.** Specs, ADRs, and runbooks live in a clean,
  predictable directory layout with consistent metadata.
- **Validate the repo.** Schemas and link checks ensure that work stays
  connected, and that “done” stays provable.
- **Operate in any repo.** Workbench is repo-agnostic. Point it at any Git
  repository and it can scaffold or validate your workflow.
- **Voice-driven capture.** Record an idea or a spec, transcribe it, and let
  Workbench generate a work item or doc for you.

## Why it feels different

Most systems are designed around tickets and dashboards. Workbench is designed
around *context*. Every work item is a narrative with a link to the spec, the
decision, the code, and the PR. This is especially important when using agents:
shared context in the repo makes automation safer and more repeatable.

Workbench also respects the developer loop. It doesn’t ask you to context-switch
into a browser, or copy/paste links between tools. It keeps work local, where you
already are.

## Who Workbench is for

- Teams who want a lightweight, repo-first workflow.
- Builders who value long-term continuity and portability.
- Agent-driven teams who need deterministic context for automation.
- Anyone who wants to stop losing the “why” behind their decisions.

## What’s next

This is the beginning. The aim is to make Workbench the most compelling way to
ship work that remains legible months later. That means investing in better
onboarding, sharper examples, and richer proof.

If you want a calmer, more durable workflow, Workbench is ready.

## Get started

- Run `dotnet run --project src/Workbench/Workbench.csproj -- --help` to explore
  the CLI.
- Review the CLI contract in `docs/30-contracts/cli-help.md`.
- Inspect the work item templates in `docs/70-work/templates/` and create your
  first task.
