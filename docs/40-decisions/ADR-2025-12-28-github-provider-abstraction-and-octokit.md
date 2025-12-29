---
workbench:
  type: adr
  workItems: []
  codeRefs: []
owner: platform
status: accepted
updated: 2025-12-28
---

# ADR-2025-12-28: GitHub provider abstraction with Octokit default

- Status: accepted
- Date: 2025-12-28
- Owner: platform

## Context

Workbench currently shells out to the GitHub CLI (`gh`) for all GitHub
integration. This is reliable but serial, process-heavy, and harder to
parallelize for bulk sync workflows. The roadmap also requires a provider
abstraction to support non-GitHub sources in the future.

## Decision

Introduce a GitHub provider abstraction with two implementations:

- `octokit` (default): use Octokit for GitHub API access with token-based
  authentication loaded from `.workbench/credentials.env` or `.env` (via
  environment variables).
- `gh`: retain the existing GitHub CLI integration for parity and as a
  fallback.

Provider selection is controlled via `github.provider` in config, defaulting
to `octokit`. The Octokit provider will be optimized for bulk operations and
will prefer batched or concurrent requests.

## Alternatives considered

- Keep `gh` as the only provider (insufficient for parallel bulk sync).
- Direct HTTP + custom models (reinvents Octokit and increases maintenance).
- Separate provider abstractions for non-GitHub sources now (premature without
  concrete use cases).

## Consequences

- Workbench must document token-based authentication and avoid logging secrets.
- `gh` remains supported but is no longer the default.
- Some provider-specific capability gaps may exist temporarily and will be
  tracked and closed as Octokit support matures.

## Related specs
- </docs/10-product/feature-spec-work-item-sync.md>

## Related work items
- none
