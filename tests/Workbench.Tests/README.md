# Workbench.Tests

This project carries the fast unit-test layer for `Workbench`.

## Test Conventions

- Use positive tests for expected happy-path behavior and positive fixtures.
- Use negative tests for rejection paths, schema failures, and diagnostic cases.
- Keep fuzz-style random-input tests in `ParserFuzzTests.cs` for parser and validator surfaces.
- Tag focused tests with `TestCategory("Positive")`, `TestCategory("Negative")`, or `TestCategory("Fuzz")` when the category helps filtering or coverage review.

## Mutation

- [`../../stryker-config.json`](../../stryker-config.json) is the repo-level mutation-test entry point for this project.
- The repo does not run mutation tests automatically yet, but the config is here so the surface is defined and ready for future wiring.

## Fuzz

- The dedicated out-of-process fuzz harness lives in [`../../fuzz/`](../../fuzz/).
- The randomized MSTest loops remain useful as a fast smoke layer even when the harness exists.

## Related Surfaces

- Permanent performance benchmarks live under [`../../benchmarks/`](../../benchmarks/).
