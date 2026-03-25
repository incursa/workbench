---
artifact_id: ARC-WB-0002
artifact_type: architecture
title: "GitHub provider abstraction with Octokit default"
domain: WB
status: approved
owner: platform
satisfies:
  - REQ-CLI-GITHUB-0001
  - REQ-CLI-GITHUB-0002
  - REQ-CLI-GITHUB-0003
  - REQ-CLI-GITHUB-0004
  - REQ-CLI-GITHUB-0005
  - REQ-CLI-GITHUB-PR-0001
  - REQ-CLI-GITHUB-PR-0002
  - REQ-CLI-GITHUB-PR-0003
  - REQ-CLI-GITHUB-PR-0004
  - REQ-CLI-GITHUB-PR-0005
  - REQ-CLI-GITHUB-PR-CREATE-0001
  - REQ-CLI-GITHUB-PR-CREATE-0002
  - REQ-CLI-GITHUB-PR-CREATE-0003
  - REQ-CLI-GITHUB-PR-CREATE-0004
  - REQ-CLI-GITHUB-PR-CREATE-0005
  - REQ-CLI-GITHUB-PR-CREATE-0006
  - REQ-CLI-GITHUB-PR-CREATE-0007
  - REQ-SYNC-0001
  - REQ-SYNC-0002
  - REQ-SYNC-0003
  - REQ-SYNC-0004
  - REQ-SYNC-0005
related_artifacts:
  - SPEC-CLI-GITHUB
  - SPEC-CLI-GITHUB-PR
  - SPEC-CLI-GITHUB-PR-CREATE
  - SPEC-SYNC-WORK-ITEM-SYNC
  - VER-WB-0001
---

# ARC-WB-0002 - GitHub provider abstraction with Octokit default

## Purpose

Workbench currently shells out to the GitHub CLI (`gh`) for all GitHub
integration. This is reliable but serial, process-heavy, and harder to
parallelize for bulk sync workflows. The roadmap also requires a provider
abstraction to support non-GitHub sources in the future.

## Requirements Satisfied

- [`REQ-CLI-GITHUB-0001`](../../requirements/CLI/SPEC-CLI-GITHUB.md)
- [`REQ-CLI-GITHUB-0002`](../../requirements/CLI/SPEC-CLI-GITHUB.md)
- [`REQ-CLI-GITHUB-0003`](../../requirements/CLI/SPEC-CLI-GITHUB.md)
- [`REQ-CLI-GITHUB-0004`](../../requirements/CLI/SPEC-CLI-GITHUB.md)
- [`REQ-CLI-GITHUB-0005`](../../requirements/CLI/SPEC-CLI-GITHUB.md)
- [`REQ-CLI-GITHUB-PR-0001`](../../requirements/CLI/SPEC-CLI-GITHUB-PR.md)
- [`REQ-CLI-GITHUB-PR-0002`](../../requirements/CLI/SPEC-CLI-GITHUB-PR.md)
- [`REQ-CLI-GITHUB-PR-0003`](../../requirements/CLI/SPEC-CLI-GITHUB-PR.md)
- [`REQ-CLI-GITHUB-PR-0004`](../../requirements/CLI/SPEC-CLI-GITHUB-PR.md)
- [`REQ-CLI-GITHUB-PR-0005`](../../requirements/CLI/SPEC-CLI-GITHUB-PR.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0001`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0002`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0003`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0004`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0005`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0006`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-CLI-GITHUB-PR-CREATE-0007`](../../requirements/CLI/SPEC-CLI-GITHUB-PR-CREATE.md)
- [`REQ-SYNC-0001`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0002`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0003`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0004`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)
- [`REQ-SYNC-0005`](../../requirements/SYNC/SPEC-SYNC-WORK-ITEM-SYNC.md)

## Design Summary

Introduce a GitHub provider abstraction with two implementations:

- `octokit` (default): use Octokit for GitHub API access with token-based
  authentication loaded from `.workbench/credentials.env` or `.env` (via
  environment variables).
- `gh`: retain the existing GitHub CLI integration for parity and as a
  fallback.

Provider selection is controlled via `github.provider` in config, defaulting
to `octokit`. The Octokit provider will be optimized for bulk operations and
will prefer batched or concurrent requests.

## Key Components

- Provider abstraction for GitHub operations
- Octokit as the default API implementation
- `gh` as a compatibility and fallback provider
- Token-based authentication sourced from local credentials

## Data and State Considerations

Provider choice comes from local config and must support bulk operations without leaking secrets to logs.

## Edge Cases and Constraints

- Bulk sync benefits from batched or concurrent requests.
- Fallback behavior is needed when Octokit capabilities lag a command surface.
- Auth failures should be reported without exposing tokens.

## Alternatives Considered

- Keep `gh` as the only provider (insufficient for parallel bulk sync).
- Direct HTTP + custom models (reinvents Octokit and increases maintenance).
- Separate provider abstractions for non-GitHub sources now (premature without
  concrete use cases).

## Risks

- Workbench must document token-based authentication and avoid logging secrets.
- `gh` remains supported but is no longer the default.
- Some provider-specific capability gaps may exist temporarily and will be
  tracked and closed as Octokit support matures.

## Open Questions

- None.
