---
artifact_id: VER-WB-0006
artifact_type: verification
title: "Public Release Support"
domain: WB
status: passed
owner: platform
verifies:
  - REQ-WB-RELEASE-0001
  - REQ-WB-RELEASE-0002
  - REQ-WB-RELEASE-0003
  - REQ-WB-RELEASE-0004
  - REQ-WB-RELEASE-0005
related_artifacts:
  - SPEC-WB-PUBLIC-RELEASE
  - ARC-WB-0007
---

# VER-WB-0006 - Public Release Support

## Scope

Launch narrative, getting-started content, demo assets, packaging guidance, and public proof points.

## Requirements Verified

- [`REQ-WB-RELEASE-0001`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0002`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0003`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0004`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`REQ-WB-RELEASE-0005`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)

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

- [`README.md`](../../../README.md)
- [`overview.md`](../../../overview.md)
- [`layout.md`](../../../layout.md)
- [`authoring.md`](../../../authoring.md)
- [`specs/requirements/WB/SPEC-WB-PUBLIC-RELEASE.md`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- dotnet pack [`src/Workbench/Workbench.csproj`](../../../src/Workbench/Workbench.csproj) -c Release
- dotnet publish [`src/Workbench/Workbench.csproj`](../../../src/Workbench/Workbench.csproj) -c Release -r osx-arm64
- artifacts/Incursa.Workbench.0.1.0.nupkg
- src/Workbench/bin/Release/net10.0/osx-arm64/publish/Workbench
- benchmark: not-applicable

## Status

The status below applies to every requirement listed in `verifies`. If the requirements do not share one outcome, split them into separate verification artifacts.

passed

## Related Artifacts

- [`SPEC-WB-PUBLIC-RELEASE`](../../requirements/WB/SPEC-WB-PUBLIC-RELEASE.md)
- [`ARC-WB-0007`](../../architecture/WB/ARC-WB-0007-workbench-boundaries.md)
