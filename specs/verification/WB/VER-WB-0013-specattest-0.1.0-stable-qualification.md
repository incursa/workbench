---
artifact_id: VER-WB-0013
artifact_type: verification
title: SpecAttest Standard 0.1.0 stable-release qualification
domain: WB
status: planned
owner: platform
verifies:
  - REQ-WB-PROVIDER-0010
related_artifacts:
  - SPEC-WB-RESOURCE-PROVIDERS
  - ARC-WB-0010
  - WI-WB-0030
  - VER-WB-0012
---

# VER-WB-0013 - SpecAttest Standard 0.1.0 stable-release qualification

## Status

**PLANNED — ENTRY BASELINE PASSED; CANDIDATE SOAK IN PROGRESS.**

This record does not approve or publish stable `0.1.0`. The earliest decision
point is 2026-12-01T21:10:39Z after seven complete days of RC1 operation.

## Scope

Determine whether the exact immutable `0.1.0-rc.1` contract can be promoted to
stable `0.1.0` without changing normative interpretation or validation
behavior. Qualification covers the frozen release bundle, independent
Workbench verification, Manager REST/MCP parity, the live LXC pin, the soak
window, human behavioral review, and publication reproducibility.

## Requirements Verified

- [`REQ-WB-PROVIDER-0010`](../../requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)

The qualification also evaluates the external Standard requirements
`REQ-VER-0005`, `REQ-VER-0009`, `REQ-VER-0011`, `REQ-VER-0012`,
`REQ-VER-0014`, `REQ-VER-0018`, `REQ-VER-0019`, and `REQ-VER-0021` without
claiming they are Workbench-owned requirements.

## Promotion Rule

Stable `0.1.0` may be published only if it retains the exact RC1 normative
payload, schemas, requirement coordinates, canonicalization behavior, and
conformance corpus. Any required normative or validator-behavior correction
forces a new candidate such as `0.1.0-rc.2`; it cannot be folded directly into
the stable release.

## Preconditions

- Signed candidate tag `standard-v0.1.0-rc.1` is published.
- Manager production is the LXC authority and advertises the complete RC1 pin.
- Workbench is pinned to the complete RC1 tuple.
- The candidate remains owner-governed and is not represented as stable.

## Entry Baseline — 2026-11-25

| Gate | Result | Evidence |
| --- | --- | --- |
| Signed tag | Passed | Local SSH verification for `samuel@mcaravey.pro`, fingerprint `SHA256:kOwAPw/gfeAOI0sTToyTAtkg/KI1kQRkCdH/SZvONKk` |
| Release assets | Passed | Every downloaded RC1 asset matched the published `SHA256SUMS` |
| Workbench edition verification | Passed | 12 specifications, 235 requirements, 20 corpus cases; validation subject `7ee6d7a166c8d8d83cebb5da19bd9aa20fd35d23c5640e5946ac205b6ca04af2` |
| Workbench corpus runner | Passed | 20/20 cases |
| Workbench repository gates | Passed | Formatting clean, Release build 0 warnings/errors, 298/298 tests |
| Standard repository validation | Passed | 34 artifacts, 309 requirements |
| Manager REST/MCP matrix | Passed | Five vectors, ten equivalent transport observations; evidence SHA-256 `d8552eb9043600cb371e3d915a6247e85dec0c08a36642ef69d9f01ce1d5f7a3` |
| Live production | Passed | Readiness HTTP 200; Manager `0.0.0-ci.168`; read-write; complete RC1 pin |

The Manager evidence validation-subject SHA-256 is
`d39abdea846f06af91c45e593e51ab3f22de3cac10502a5aec50dd7c3f14a819`.
No production mutation or persistent qualification credential was used.

## Soak Observations

### 2026-11-25 — production pin and Workbench repeat

- `https://app.specattest.com/health/ready` and
  `https://spectrace.incursa.com/health/ready` returned HTTP 200.
- Both canonical production hostnames returned byte-identical, sorted `/version`
  documents with SHA-256
  `5e83dcf82a9ca5abeed77dfe9e5c5e85c5f9f0d3e408165849482e393835c647`.
- The live Manager remained `0.0.0-ci.168`, read-write, and pinned to the
  complete `0.1.0-rc.1` tuple, including source commit, manifest, schemas,
  payload, corpus, signed tag, canonicalization, and profiles.
- Workbench `main` at `ebc1acef2ae7accf880219a4d247709b6b07ed46`
  retained the same complete tuple. The Release configuration run of
  `Workbench.SpecAttestProvider.Tests` passed 22/22 tests, including the RC1
  conformance corpus and fail-closed pin checks.
- No normative, interoperability, integrity, or rollback incident was found.

## Soak and Exit Gates

| Gate | State | Pass condition |
| --- | --- | --- |
| Seven-day soak | Open | RC1 operates through 2026-12-01T21:10:39Z |
| Incident review | Open | No unresolved normative, interoperability, integrity, or rollback event |
| Final cross-tool rerun | Open | Standard, Workbench, Manager, release assets, and live pin all pass again |
| Behavioral review | Open | Human review confirms zero normative or validator-behavior differences from RC1 |
| Owner approval | Open | Incursa owner explicitly approves stable publication |
| Stable freeze and redownload | Open | Signed `standard-v0.1.0` and all immutable assets reproduce independently |
| Downstream stable pins | Open | Manager and Workbench adopt and qualify the complete published stable tuple |

## Procedure

1. Observe the entire soak window and retain incident/issue findings.
2. At the decision point, independently redownload and hash the RC1 assets.
3. Re-run Workbench edition and corpus verification against the signed RC1
   commit, then run Workbench formatting, Release build, and tests.
4. Re-run the RC1-specific Manager REST/MCP protocol qualification and verify
   the live LXC pin and operating mode.
5. Compare the intended stable payload, schemas, corpus, coordinates, and
   canonicalization with RC1 byte for byte.
6. Complete the behavioral review and request explicit owner approval.
7. Only after approval, build and sign the immutable stable edition, publish
   it as a non-prerelease, redownload it independently, and update downstream
   pins.

## Expected Result

Either all gates pass and the unchanged RC1 contract is eligible for explicit
owner-approved stable publication, or the record identifies a concrete blocker
and directs the project to another release candidate.

## Evidence

- [RC1 publication](https://github.com/incursa/spec-trace/releases/tag/standard-v0.1.0-rc.1)
- [RC1 deployment qualification](https://github.com/incursa/spec-trace-manager/blob/main/docs/qualification/specattest-0.1.0-rc.1-lxc-deployment.md)
- [RC1 protocol qualification](https://github.com/incursa/spec-trace-manager/blob/main/docs/qualification/specattest-0.1.0-rc.1-rest-mcp.md)
- [Prior candidate qualification](./VER-WB-0012-specattest-draft-qualification.md)
- `benchmark: not-applicable`

## Status

`planned` — the entry baseline passed, but the time-bound soak and final
approval/publication gates remain open.

## Related Artifacts

- `WI-WB-0030`
- `VER-WB-0012`
- `SPEC-WB-RESOURCE-PROVIDERS`
- `ARC-WB-0010`
