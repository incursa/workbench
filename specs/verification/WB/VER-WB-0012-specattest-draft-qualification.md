# Workbench qualification against SpecAttest Standard 0.1.0-draft.1

Original draft.1 status: **NOT READY**

Qualification date: 2026-11-23/24 UTC

Current follow-up status (2026-11-24 UTC): **RC.1 PUBLISHED; WORKBENCH PIN
QUALIFIED**. The original `0.1.0-draft.1` findings below are retained as the
behavioral-review record. The draft.2 implementation, deployment, package
publication, retained cross-transport evidence, and owner-approved candidate
publication close the release-candidate gates described below.

## Qualification subjects

- Workbench baseline: `df1e2a6` (`feat(providers): add provider-neutral resource integrations`)
- Workbench qualification fix: `69c88ef` (`fix(specattest): enforce immutable draft edition tuple`)
- Standard edition: `0.1.0-draft.1`
- Standard source: `incursa/spec-trace@569569f7ffb2b68a45e4b5c3c276951404973470`
- Standard release: `standard-v0.1.0-draft.1`
- Manager: `0.0.0-ci.163`, source `23ec7f28ea113fdd747130098682ed1cccb817d2`
- Manager endpoint: `https://spectrace.incursa.com`

The Standard source and release were treated as immutable. This review did not advance, edit, or republish the Standard.

## Edition tuple and release verification

The live Manager `/version`, authenticated REST interoperability capability, MCP `get_server_info`, and MCP `get_safe_mutation_capabilities` responses agreed on the following values:

| Coordinate | Verified value |
| --- | --- |
| Edition | `0.1.0-draft.1` |
| Commit | `569569f7ffb2b68a45e4b5c3c276951404973470` |
| Edition manifest | `6b54ca666d90456ae7edb277114945fcc4163e6084a3f44e913fc19e40691fda` |
| Model schema | `27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb` |
| Edition-manifest schema | `456de53b5359691b21540bd2803529119930c0db507d760060a224f42e0a7364` |
| Payload | `288593ff470fc5661a370c22377ec7c7503ae24a66f50e014e1f42b0426f3db4` |
| Conformance corpus | `8d6a2d02d5293ca531aee9eaa9aff771dceb88d34f6d8dd45c396710e0881d33` |
| Signed tag | `refs/tags/standard-v0.1.0-draft.1` |
| Tag object | `c0c4e93600a803a3c3f48780e307666cfd76f7122a155c19e416f21774f6d056` |

The annotated tag points to the exact source commit. Local SSH signature verification succeeded for `samuel@mcaravey.pro`, key fingerprint `SHA256:kOwAPw/gfeAOI0sTToyTAtkg/KI1kQRkCdH/SZvONKk`. Independent hashing reproduced every tuple digest. A branch name such as `main`, a changed commit, payload, manifest, schema, corpus, or signature reference is rejected by the corrected Workbench SpecAttest provider configuration.

The live Manager advertises `specattest.manager-safe-mutation.v1` with `stage`, `inspect`, `commit`, `cancel`, and `resume`; required idempotency and expected versions; atomic commit; no automatic merge; immutable audit/version history; and deterministic retry.

## Commands and suites executed

Representative commands (temporary paths omitted where not material):

```text
curl -sS https://spectrace.incursa.com/version
git fetch --tags https://github.com/incursa/spec-trace
git verify-tag standard-v0.1.0-draft.1
git cat-file tag standard-v0.1.0-draft.1 | shasum -a 256
shasum -a 256 standard-edition.json model/model.schema.json schemas/standard-edition.schema.json
git archive --format=tar 569569f... conformance/versioning | shasum -a 256
python3 tools/independent-standard-validator.py ...
dotnet run -p:RollForward=Major --project tools/SpecTrace.Tool ...
dotnet format Workbench.slnx
dotnet build Workbench.slnx -c Release
dotnet test --solution Workbench.slnx
```

Authenticated REST requests exercised `/api/v1/interoperability/specattest`, project canonical export, and all five safe-mutation operations. Raw MCP JSON-RPC exercised `initialize`, `tools/list`, `get_safe_mutation_capabilities`, `stage_safe_mutation`, `get_safe_mutation`, and `commit_safe_mutation`.

Baseline results before qualification changes were 286 Workbench tests passed and Release build passed with zero warnings/errors. The focused corrected provider suite passed 11/11. The final repository gates are recorded below.

The optional strict diagnostics profile (`AnalysisMode=All`, generated XML documentation, warnings as errors) reported 1,532 existing repo-wide analyzer/documentation findings; the repository's required Release build did not report them.

### Final repository gates

- `dotnet format Workbench.slnx --verify-no-changes`: passed.
- `dotnet build Workbench.slnx -c Release`: passed, zero warnings and errors.
- `dotnet test --solution Workbench.slnx`: passed, 287/287 tests.
- `scripts/testing/run-quality-evidence.ps1`: all 287 tests passed and evidence was generated.
- `workbench quality show`: advisory failure; line coverage 22.3% versus 55% intent, branch coverage 19.6% versus 50% intent, and three critical files below their authored thresholds.
- `workbench validate --profile auditable`: failed on extensive pre-existing downstream-trace, reciprocity, body-consistency, and orphan-artifact findings. These findings are independent additional release gates; they were not relaxed during qualification.

## Independent Standard validation and corpus

Both the published .NET candidate validator and the independent Python validator accepted the manifest and canonical payload: 5 corpus cases, 12 specifications, and 228 requirement revisions. Their reproduced report hashes matched the manifest (`f695c41532d303e51773937b95f02c2654fa32a10be183292e9ebef48de72d74` and `e7aaddac655ecce4dece481436680a0548b5f74110419355fe52b01e86a19b4c`).

| Fixture | Expected | Independent result | Workbench result |
| --- | --- | --- | --- |
| `valid-bootstrap-draft.json` | accept | accept | no edition-manifest/corpus runner |
| `invalid-draft-as-stable.json` | reject | reject | no edition-manifest/corpus runner |
| `invalid-zero-revision.json` | reject | reject | no edition-manifest/corpus runner |
| `invalid-zero-prerelease-ordinal.json` | reject | reject | no edition-manifest/corpus runner |
| `invalid-successor-without-review.json` | reject | reject | no edition-manifest/corpus runner |

This is an open Workbench implementation gate. Provider tuple checking is not a substitute for independently validating the edition manifest, canonical payload, and corpus.

## Requirement revisions and interchange

The corrected Workbench SpecAttest interchange layer retains JSON content without remapping `id`, `revision`, `trace.supersedes`, or extension fields. A focused test preserves requirement `REQ-DEMO-0001` at revision `7` with lineage. Deterministic archive generation and canonical object-key serialization pass.

End-to-end qualification nevertheless failed:

- Workbench Core still embeds pre-edition schema commit `79371b948e6bb83ca25136490d18615ad3ebf704`; changing it to the published model schema caused existing editor and validation tests to fail broadly, including missing `revision`, identifier patterns, normative-clause shape, and artifact fields.
- Workbench has Manager-export-to-custom-interchange support, but no Standard artifact import into the local repository, no reverse export back to Standard artifacts, and no semantic/canonical round-trip command.
- The provider marks Manager exports unattested and cannot accept them for an authority transition.
- Manager safe intake accepted records containing input revisions `7`, `9`, and `11`, but canonical export rewrote each to `revision: 1` (its record version). Stable IDs and `trace.supersedes` survived when supplied through the Manager-native trace field. Exact revision did not survive.
- Manager history uses storage versions, but the observed import proves its normative requirement revision is currently coupled to storage `recordVersion`; this violates the Standard's distinct-coordinate model.

Fields observed as changed or lost in the Manager round trip: normative `revision` was reinterpreted; an unsupported top-level `supersedes` was dropped, while canonical `trace.supersedes` was preserved; timestamps and Manager operational fields were normalized/added; object keys were canonically reordered. IDs, statement text, and canonical lineage survived the correctly shaped case.

## REST/MCP agreement

REST and raw MCP returned the same complete Standard pin and safe-mutation guarantees. A mutation staged and committed through MCP was inspected through REST with the same import-run ID, status, operation version, and request digest. REST and MCP therefore agree for the exercised deployed contract.

The installed Codex SpecAttest connector schema exposed legacy intake tools but omitted the six deployed safe-mutation MCP tools even though raw MCP `tools/list` returned all six. This is a connector/tool-catalog deployment gap, not a Manager protocol disagreement.

Workbench does not integrate with MCP. Its Manager client uses REST reads only and currently advertises a read-only provider role; mutation planning/apply fail closed with `specattest.manager.safe-mutation.unavailable`.

The Standard's five-fixture corpus tests edition-versioning rules only. It contains no Manager REST/MCP validation fixtures, so the requested claim that both tools accept/reject the same full corpus cannot be established from the published corpus.

## Safe mutation results

| Behavior | Result |
| --- | --- |
| Stage without idempotency key | rejected `400` |
| Stage without positive expected project version | rejected `400` |
| Wrong Standard pin | rejected `409` |
| Inspect staged operation | passed; no authored requirement existed |
| Stale operation version | rejected `409`; staged state unchanged |
| Atomic commit | passed for tested single-requirement batches |
| Commit retry | deterministic; same completed operation/version |
| Cancel retry | deterministic; same canceled operation/version |
| Canceled operation partial state | none observed |
| Resume after cancel/interruption | passed; one atomic commit |
| Automatic merge | none observed; stale conflict rejected |
| Audit/version immutability | hash-chained audit events and increasing immutable operation versions observed |
| Stage retry with exact HTTP body | **failed unless client supplies fixed timestamps**; same key/body conflicted because server-created default timestamps changed the request digest |
| Exact normative revision | **failed**; imported revision was rewritten to Manager record version |

The live qualification project and temporary API credential were removed after evidence collection.

## Fixes made

Commit `69c88ef`:

- expands the provider-neutral compatibility pin so the SpecAttest adapter can carry the full immutable edition tuple;
- makes SpecAttest pin validation require exact manifest, schemas, payload, corpus, signed-tag, canonicalization, and profile coordinates;
- rejects branch-head substitution and tuple drift;
- compares list-valued pin coordinates structurally;
- adds revision/lineage preservation and tamper tests;
- extends the provider protocol schema for the added coordinates.

No Standard or Manager source was changed.

## Differences, classification, and open gates

### Workbench implementation defects

1. Core document validation/editor generation is not conformant to the published model schema.
2. There is no independent edition-manifest, canonical-payload, or conformance-corpus runner.
3. There is no Standard artifact import/export and no end-to-end semantic/canonical round trip.
4. The SpecAttest provider does not verify the live Manager `/version` tuple before reads.
5. The provider does not implement the advertised REST safe-mutation contract and does not integrate with MCP.
6. Manager exports remain explicitly unattested, so Workbench cannot perform an authority transition.

### Manager implementation defects

1. Normative requirement revision is regenerated from Manager `recordVersion`, losing imported exact revisions.
2. Deterministic stage replay depends on callers supplying fixed operational timestamps that are not part of the logical mutation; an identical HTTP body without them can conflict.

### Draft Standard gaps/ambiguities

1. The corpus covers edition versioning only, not requirement interchange, canonical round trips, or safe-mutation protocol agreement.
2. The Standard defines revision semantics but no normative Manager/interchange envelope or mapping rule separating normative revision from provider storage version.
3. The edition pin is described across manifest/schema/release artifacts, but a single normative verification algorithm and negative substitution corpus are not yet supplied.

For `0.1.0-draft.2`, propose:

- Revise `REQ-VER-0002` to revision 2: accepted edits increment the normative revision, while import of immutable content MUST retain its supplied positive revision and MUST NOT derive it from a provider storage/concurrency version.
- Add `REQ-VER-0022` revision 1: interchange and authority-transfer implementations MUST preserve stable ID plus exact revision and model provider concurrency coordinates separately.
- Add `REQ-SCH-0035` revision 1: an edition verifier MUST validate the complete pin tuple, signed immutable reference, manifest/schema/payload/corpus hashes, and MUST reject branch references or partial tuples.
- Add an authoritative `specattest-interchange.schema.json` separating `revision`, `expectedProjectVersion`, and `expectedOperationVersion`.
- Add corpus fixtures for wrong commit, branch substitution, altered manifest/schema/payload/corpus/tag, revision-preserving import/export, revision/storage-version divergence, lineage preservation, canonical reorder equivalence, stale commit, atomic failure, deterministic replay, cancel, and resume; run the same vectors over REST and MCP.

These are proposals only; this qualification did not change the immutable draft.

## Original draft.1 recommendation

**NOT READY for SpecAttest Standard 0.1.0-rc.1.**

The gates are: migrate Workbench Core to the published schema; implement independent edition/corpus validation; implement Standard import/export round trips; implement and qualify REST safe mutation in Workbench; fix Manager revision preservation and timestamp-independent idempotent replay; add the missing interoperability/conformance fixtures; then rerun full Workbench formatting, Release build, tests, Spec Trace validation, and quality evidence with a clean worktree.

## Implementation follow-up: 2026-11-24 UTC

Workbench subsequently closed these adapter-specific gates on the
`codex/specattest-draft2-gates` branch as a follow-up to
[WI-WB-0029](../../work-items/WB/WI-WB-0029-first-party-resource-provider-adapters.md):

- canonical Manager and intake reads now verify the live `/version` Standard
  pin against every configured immutable coordinate before accepting content;
- the Manager client verifies the deployed safe-mutation capability contract
  and implements REST stage, inspect, commit, cancel, and resume;
- Manager-authority provider planning requires the exact authority epoch,
  project version, attested interchange payload, and unchanged artifact set;
- apply uses Manager intake concurrency coordinates and the staged atomic
  safe-mutation contract;
- the intake mapping sends the normative top-level requirement `revision`
  independently from Manager `recordVersion`;
- deterministic ZIP import/export now rejects noncanonical or digest-mismatched
  archives and round-trips exact revision and `trace.supersedes`; and
- focused tests exercise pin drift rejection, all safe-mutation REST routes,
  atomic provider plan/apply, and exact revision/lineage round trips.

### Draft.2 gate closure: 2026-11-24 UTC

Workbench now pins the immutable `0.1.0-draft.2` edition tuple from
`incursa/spec-trace@3dedb948549ee3bc07adc7360e80f9c4125076a0`. The model,
manifest, manifest-schema, payload, corpus, signed-tag reference, and tag-object
digests are asserted independently in the provider test suite.

The remaining adapter-specific Workbench gates were closed as follows:

- Core embeds the model schema from the frozen draft.2 commit. Canonical JSON
  fixtures and the specification editor now require, load, preserve, and emit
  each positive normative requirement `revision`; Manager `recordVersion`
  remains a separate provider concurrency coordinate.
- `workbench-specattest-provider verify-edition <root> <manifest>` independently
  validates the edition-manifest schema, full trusted pin, manifest and schema
  bytes, canonical requirement coordinates and digests, ordered payload digest,
  validator roles, signed reference, and commit-addressed corpus archive.
- `workbench-specattest-provider verify-corpus <directory>` evaluates all
  versioning, pin-substitution, interchange, canonical-reordering, and safe
  mutation vectors without requiring a live Manager.
- The verifier accepted the exact frozen manifest (`885cb516...`) with 12
  specifications, 235 requirement revisions, and all 20 corpus cases. Its
  validation-subject digest was
  `8b61f54c67494f2ffeb1d3231dadd3066df5ff547b9cf2ea90652f08b5f4cdcb`,
  agreeing with the independent Standard validator.
- Published protocol-vector tests cover stale and atomic rejection, deterministic
  replay, cancel, resume, normative revision/storage-version divergence, and
  canonical object-key reorder equivalence.

Final Workbench gates for this follow-up passed:

- `dotnet format Workbench.slnx --verify-no-changes`
- `dotnet build Workbench.slnx -c Release --no-restore` with zero warnings and
  errors
- `dotnet test --solution Workbench.slnx -c Release --no-build --no-restore`
  with 298/298 tests passed

This closes the identified Workbench implementation gaps.

### Deployed cross-tool evidence: 2026-11-24 UTC

The public Manager deployment subsequently closed the remaining deployed-tuple
gate:

- `https://spectrace.incursa.com/health/ready` returned HTTP 200.
- `/version` reported Manager `0.0.0-ci.165` from source
  `01df7945e11980bb68ac8132cb7dd2585b537f24` and the complete immutable
  `0.1.0-draft.2` tuple matched Workbench exactly.
- Workbench `verify-edition` passed the frozen bundle with 12 specifications,
  235 requirement revisions, 20 corpus cases, and validation-subject digest
  `8b61f54c67494f2ffeb1d3231dadd3066df5ff547b9cf2ea90652f08b5f4cdcb`.
- Workbench `verify-corpus` passed all 20/20 published cases independently of
  the live Manager.
- The container's service identity launched sandboxed Chromium successfully,
  the public readiness endpoint returned HTTP 200 after restart, and the
  Manager retained the qualified state across that service restart.

The repo-local implementation gates and deployed immutable-tuple gates are now
closed.

### Distribution gate closure: 2026-11-24 UTC

Workbench package publication no longer depends on the expired organization-wide
NuGet API key. NuGet Trusted Publishing now grants a short-lived credential only
to `incursa/workbench`, workflow `nuget-publish.yml`, for new versions of
`Incursa.Workbench`.

- Workbench PR `#622` merged the OIDC-based publishing workflow at main commit
  `8523bd338745b65b5f4a4f8fe0c98e1c1cbd7c5b`.
- GitHub Actions run `45452275174` completed successfully, including all six AOT
  targets, release assembly, reporting, and the NuGet job.
- NuGet accepted `Incursa.Workbench.2026.11.24.1066.nupkg` and its symbol
  package. Both the NuGet flat-container and registration indexes subsequently
  reported `2026.11.24.1066`, and the public package page returned HTTP 200.

This closes the package-distribution gate without rotating or introducing a
long-lived NuGet secret.

## RC readiness double-check: 2026-11-24 UTC

The draft.2 release explicitly records this known gap:

> REST and MCP implementations must record their results against the shared
> protocol vectors before release-candidate promotion.

Here, **record their results** means retaining a machine-readable result matrix,
with tool/version identity and a digest, showing that REST and MCP each produced
the declared outcome for the five published safe-mutation vectors:

| Published vector | Required REST result | Required MCP result |
| --- | --- | --- |
| `protocol-stale-commit` | rejected, no partial mutation | rejected, no partial mutation |
| `protocol-atomic-failure` | rejected, no partial mutation | rejected, no partial mutation |
| `protocol-deterministic-replay` | staged deterministically | staged deterministically |
| `protocol-cancel` | canceled, no partial mutation | canceled, no partial mutation |
| `protocol-resume` | committed atomically | committed atomically |

This evidence does **not** require a persistent production credential, mutation
of production data, Kubernetes, or a continuously running qualification job. An
authenticated disposable integration environment is sufficient if it runs the
frozen corpus bytes through the real REST and MCP bindings and retains the
result artifact.

Manager now retains the requested machine-readable result artifact at the
immutable source commit
[`c2f8ae7fde399d2b6dcb6748aaa2c3f1bb2d610d`](https://github.com/incursa/spec-trace-manager/blob/c2f8ae7fde399d2b6dcb6748aaa2c3f1bb2d610d/docs/qualification/specattest-0.1.0-draft.2-rest-mcp-results.json).
Its whole-file SHA-256 is
`32874adfc64d33cf1bd45a8554c48b5da02b0482168e41009fe60de53213f529`;
the canonical validation-subject SHA-256 is
`97186df819d0d63f2094b775ea80c14adc1a73e26e782b68dde1e6259ee5ccee`.

The disposable authenticated harness executed the exact frozen input bytes for
all five named protocol cases through the real REST and MCP bindings. All 10
transport observations matched their declared outcomes, including rejection
without partial mutation, deterministic replay, cancellation without mutation,
and atomic resume/commit. A second generated run reproduced the same evidence
hash. The retained record identifies the qualified Standard and Manager source,
and retains no credential, timestamp, random identifier, production mutation,
Kubernetes dependency, or other environment-specific value.

The Manager pull-request gates also passed browser qualification, Debian LXC
package validation, and release-evidence/provenance generation. Local Release
build and applicable tests passed; the only excluded Manager tests are the two
pre-existing macOS `EphemeralKeySet` platform cases documented above.

Consequently:

- `0.1.0-draft.2` is qualified for continued Workbench and Manager use.
- Workbench implementation, Manager deployment, package distribution, and the
  retained REST/MCP protocol matrix are not RC blockers.
- No technical preparation blocker remains before creating `0.1.0-rc.1`.
- Candidate publication still requires Incursa owner approval, followed by the
  candidate build/manifest validation, signed immutable tag, GitHub prerelease,
  published checksums/results, and independent redownload verification.

Current recommendation: **TECHNICAL RC PREPARATION GATES CLOSED; OWNER APPROVAL
REQUIRED BEFORE PUBLICATION.**

## RC.1 publication and Workbench pin: 2026-11-24 UTC

Incursa owner approval was recorded and
[SpecAttest Standard `0.1.0-rc.1`](https://github.com/incursa/spec-trace/releases/tag/standard-v0.1.0-rc.1)
was published as a GitHub prerelease from signed source commit
`afb4885980bdcbe250bb18fd07948c8999450330`. The candidate manifest records
`maturity: candidate`, `bootstrap: false`, predecessor `0.1.0-draft.2`, and
`compatibility: non-normative`.

Workbench now pins the complete immutable candidate tuple:

| Coordinate | `0.1.0-rc.1` value |
| --- | --- |
| Edition manifest | `22ef8a5347aaa2b430ff5fbca8495f54a0e078ffbcc12d92d9f38389a47fbe98` |
| Model schema | `27fbfef7d11743adaa5f95b14e16b9f9e71fd9de522e3a9f7e22e62c12e88ddb` |
| Manifest schema | `eb12cb68f01bd03cb2c29ae549ca8d202a129f75c304ebcbf1b4e64a975048ef` |
| Ordered payload | `0643a8067ed5684dcbaa29da5c9a206af813c142d1a7620bce2a81bddedd4b92` |
| Corpus bundle | `b4ab6783a4693069a9eb5f0f96f01f49ae70bf56bb10ba9d9499a5e9c4b554a2` |
| Signed tag object | `de2dc61fee3b0f4137612696b910d8e6b3455011bd46222d1a8a8cfb731ec844` |
| Validation subject | `7ee6d7a166c8d8d83cebb5da19bd9aa20fd35d23c5640e5946ac205b6ca04af2` |

The normative payload, schemas, validator implementation, and 20 conformance
cases are byte-identical to draft.2. Both the accepted draft.2 validator and the
candidate validator passed all 235 exact requirement coordinates and 20 corpus
cases. The signed tag is GitHub-verified, every release asset passed its
published checksum after independent redownload, and the retained REST/MCP
matrix remains applicable because the five protocol vectors and required
behavior did not change.

Current recommendation: **ADOPT `0.1.0-rc.1` FOR CANDIDATE INTERCHANGE AND
CROSS-TOOL REVIEW; DO NOT REPRESENT IT AS A STABLE EDITION.**
