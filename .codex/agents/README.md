# .NET Subagent Baseline

These agents are a reusable baseline for .NET repositories. They are intentionally generic and should be narrowed to the solution layout and ownership boundaries of the target repo.

Recommended default set:

- `dotnet_repo_explorer`: map the solution, project graph, tests, entry points, and likely write scope before editing.
- `dotnet_impl_worker`: make bounded production-code changes in one slice of the codebase.
- `dotnet_surface_worker`: handle CLI, web, API, TUI, or other user-facing entry points.
- `dotnet_verification_worker`: run builds and tests, diagnose failures, and apply code fixes.
- `dotnet_cleanup_worker`: do broader formatting follow-up, mechanical cleanup, and small refactors.
- `dotnet_documentation_worker`: add XML docs and in-code commentary where maintainability needs it.
- `dotnet_review_checker`: review diffs for regressions, missing tests, contract drift, and layering issues.

Use the smallest set that fits the task. The surface worker is optional when the repository has no user-facing entry points.
