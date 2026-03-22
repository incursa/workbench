---
artifact_id: VER-WB-0003
artifact_type: verification
title: "Terminal UI Mode"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-TUI-0001
  - REQ-TUI-0002
  - REQ-TUI-0003
  - REQ-TUI-0004
  - REQ-TUI-0005
related_artifacts:
  - SPEC-TUI-TERMINAL-UI
  - ARC-WB-0003
---

# VER-WB-0003 - Terminal UI Mode

## Scope

Embedded terminal UI entrypoints, shared-service reuse, command preview, and safe mutation behavior.

## Requirements Verified

- REQ-TUI-0001
- REQ-TUI-0002
- REQ-TUI-0003
- REQ-TUI-0004
- REQ-TUI-0005

## Verification Method

Documentation review, repository validation, and targeted command checks.

## Preconditions

- Canonical Spec Trace artifacts are present in the repository.

## Procedure or Approach

1. Review the linked spec and architecture artifacts.
2. Run the repo validation and command-surface checks.
3. Confirm the expected files, paths, and outputs exist.

## Expected Result

The linked requirements are satisfied by the documented repository behavior and validation outputs.

## Evidence

- tests/Workbench.IntegrationTests/CommandSurfaceTests.cs
- tests/Workbench.Tests/WorkItemEditTests.cs
- src/Workbench.Tui/TuiEntrypoint.cs
- src/Workbench.Tui/TuiEntrypoint.Helpers.cs

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

passed

## Related Artifacts

- SPEC-TUI-TERMINAL-UI
- ARC-WB-0003
