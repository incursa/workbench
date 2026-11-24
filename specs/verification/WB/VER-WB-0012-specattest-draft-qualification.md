# Workbench qualification against SpecAttest Standard 0.1.0-draft.1

Status: **NOT READY**

Qualification date: 2026-11-23/24 UTC

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

## Recommendation

**NOT READY for SpecAttest Standard 0.1.0-rc.1.**

The gates are: migrate Workbench Core to the published schema; implement independent edition/corpus validation; implement Standard import/export round trips; implement and qualify REST safe mutation in Workbench; fix Manager revision preservation and timestamp-independent idempotent replay; add the missing interoperability/conformance fixtures; then rerun full Workbench formatting, Release build, tests, Spec Trace validation, and quality evidence with a clean worktree.
