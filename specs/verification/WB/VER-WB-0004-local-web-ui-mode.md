---
artifact_id: VER-WB-0004
artifact_type: verification
title: "Local Web UI Mode"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-WEB-0001
  - REQ-WEB-0002
  - REQ-WEB-0003
  - REQ-WEB-0004
  - REQ-WEB-0005
related_artifacts:
  - SPEC-WEB-LOCAL-UI
  - ARC-WB-0006
---

# VER-WB-0004 - Local Web UI Mode

## Scope

Browser-first local editing, repository browsing, and shared core-service reuse inside the Workbench executable.

## Requirements Verified

- [`REQ-WEB-0001`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0002`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0003`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0004`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`REQ-WEB-0005`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)

## Verification Method

Page-level HTTP smoke tests, repository validation, and targeted command
checks.

## Preconditions

- Canonical Spec Trace artifacts are present in the repository.

## Procedure or Approach

1. Review the linked spec and architecture artifacts.
2. Run the repo validation and command-surface checks.
3. Start the production Razor Pages host on an ephemeral loopback port.
4. Confirm the item, dashboard alias, docs, specs, files, settings, and create
   routes render successfully from a scaffolded local repository.
5. Confirm an embedded stylesheet is served without a source checkout.

## Expected Result

The linked requirements are satisfied by the documented repository behavior and validation outputs.

## Evidence

- [`src/Workbench/Pages/Index.cshtml.cs`](../../../src/Workbench/Pages/Index.cshtml.cs)
- [`tests/Workbench.Tests/WorkItemBranchCoverageTests.cs`](../../../tests/Workbench.Tests/WorkItemBranchCoverageTests.cs)
- [`tests/Workbench.IntegrationTests/CommandSurfaceTests.cs`](../../../tests/Workbench.IntegrationTests/CommandSurfaceTests.cs)
- [`tests/Workbench.IntegrationTests/WebSurfaceTests.cs`](../../../tests/Workbench.IntegrationTests/WebSurfaceTests.cs)
- benchmark: not-applicable

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

passed

## Related Artifacts

- [`SPEC-WEB-LOCAL-UI`](../../requirements/WEB/SPEC-WEB-LOCAL-UI.md)
- [`ARC-WB-0006`](../../architecture/WB/ARC-WB-0006-local-web-ui-mode.md)
