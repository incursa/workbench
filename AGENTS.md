# Repository Guidelines

## Project Structure & Module Organization

This repository is currently minimal and only contains top-level documentation. At the moment there is no `src/`, `tests/`, or asset directory. If you add code, prefer a conventional layout such as:

- `src/` for application or library source
- `tests/` for automated tests
- `assets/` or `public/` for static files

Keep related modules together and add a short README in any new top-level directory that explains its purpose.

## Build, Test, and Development Commands

No build or test commands are defined yet. When you introduce tooling, document the primary commands here and in `README.md`, for example:

- `npm run dev` for local development
- `npm test` for the test suite
- `make build` for production builds

## Coding Style & Naming Conventions

No style guide is established yet. When adding code, align with the chosen language’s standard formatter and document it here (e.g., `prettier`, `gofmt`, `black`). Prefer clear, descriptive names and consistent casing (e.g., `camelCase` for variables, `PascalCase` for types).

## Testing Guidelines

There are no tests or frameworks configured. If you add tests, describe the framework and conventions, such as:

- Test files named `*.test.*` under `tests/`
- `npm test` or `pytest` as the entry point

## Commit & Pull Request Guidelines

No repository-specific conventions are recorded yet. Use concise, imperative commit messages (e.g., "Add CLI scaffold"). For pull requests, include:

- A short summary of the change
- Links to relevant issues or context
- Screenshots or logs when behavior changes
## Agent-Specific Instructions

If you add automation or agents, document them in `AGENTS.md` and keep instructions short and actionable.
