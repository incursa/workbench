# Workbench SpecAttest provider

`workbench-specattest-provider` is an out-of-process adapter for a SpecAttest
Manager endpoint. It uses the same `workbench.resource-provider.v1` protocol as
every other Workbench resource provider and never loads Manager code into the
Workbench process.

## Current supported surface

- local loopback Manager endpoints over HTTP or HTTPS;
- hosted Manager endpoints over HTTPS;
- exact, caller-supplied `specattest.standard-pin.v1` coordinates;
- query/get translation of one Manager project to one
  `specattest.interchange.v1` specification-set snapshot;
- deterministic payload, entry-manifest, entry, evidence, and audit-reference
  digests;
- lossless canonical JSON and `x_` extension preservation;
- fail-closed validation of set authority, pin, stable IDs, scope, retired IDs,
  and digests;
- immutable, deterministic store-and-forward ZIP payloads with a JSON manifest.
- byte-for-byte canonical ZIP import/export round trips that retain normative
  requirement revisions and lineage;
- live Manager `/version` verification against the complete configured pin
  before canonical or intake reads; and
- Manager-authority `planChanges`/`applyChanges` backed by the deployed
  `specattest.manager-safe-mutation.v1` stage/inspect/commit/cancel/resume REST
  contract.

The current Manager contract does not expose an exact Standard pin and parity
receipt on canonical exports. Exported snapshots are therefore marked
`canonicalProfilePassed=false`; they are inspectable but cannot be accepted as
an authority transition.

When configured with `authority-mode=manager`, the provider advertises
`authority`, plans exactly one atomic specification-set update, obtains the
Manager intake concurrency coordinates, and stages and commits it through safe
mutation. Requirement records carry top-level normative `revision` separately
from Manager `recordVersion`. Artifact mutations remain fail-closed because the
safe-intake contract currently covers requirements, sources, and groups rather
than specification artifacts.

## Configuration

The provider process is configured for exactly one specification set and one
Manager project. This keeps the authority domain explicit and prevents wildcard
or overlapping set mappings.

```json
{
  "id": "specattest-review",
  "executable": "workbench-specattest-provider",
  "arguments": [
    "--mode", "hosted",
    "--endpoint", "https://manager.example.com/",
    "--set-id", "<specification-set-uuid>",
    "--project-id", "<manager-project-id>",
    "--authority-mode", "git-workbench",
    "--authority-provider-id", "<configured-authority-provider-id>",
    "--authority-epoch", "<immutable-authority-epoch>",
    "--standard-pin-file", ".workbench/specattest-standard-pin.json",
    "--token-env", "WORKBENCH_SPECATTEST_TOKEN"
  ]
}
```

The pin file must contain the complete tuple selected by the repository owner;
the provider has no default and never follows `main` or `latest`.

```json
{
  "contract": "specattest.standard-pin.v1",
  "repository": "https://github.com/incursa/spec-trace",
  "edition": "<edition-or-explicit-draft-label>",
  "revision": "<40-hex-commit>",
  "schemaPath": "model/model.schema.json",
  "schemaSha256": "<64-lower-hex>",
  "editionManifestSha256": "<64-lower-hex>",
  "editionManifestSchemaPath": "schemas/standard-edition.schema.json",
  "editionManifestSchemaSha256": "<64-lower-hex>",
  "payloadSha256": "<64-lower-hex>",
  "conformanceCorpusSha256": "<64-lower-hex>",
  "signatureReference": "refs/tags/<immutable-edition-tag>",
  "signatureSha256": "<64-lower-hex>",
  "canonicalization": "utf8-json-sorted-object-keys-v1",
  "profile": "core",
  "canonicalProfiles": ["core", "traceable", "auditable"],
  "proofProfile": "auditable"
}
```

Credentials are read only from the named environment variable. Never put a
bearer token in provider arguments or repository configuration.

## Authority rules

- `manager` means Manager remains the sole authored-content authority.
- `git-workbench` means the Manager project is an imported review snapshot;
  Git/Workbench remains authoritative.
- Querying or exporting never transfers authority.
- A missing or mismatched pin, authority declaration, version, digest, ID
  ledger, or validation receipt is a conflict.
- No timestamp comparison, last-writer-wins behavior, automatic merge, or
  automatic bidirectional synchronization is implemented.
- Baselines and publications remain immutable Manager coordinates. Rendered
  HTML, PDF, catalogs, and caches are derived projections.

The initial portfolio Standard tuple remains deliberately unselected. Supply
only an owner-approved exact pin; test fixtures are not adoption pins.

## Remaining upstream contract gaps

The adapter does not emulate missing lifecycle operations with CRUD calls:

- `workbench.resource-provider.v1` has no operation for staging a
  non-authoritative snapshot. Treating that transition as `create`, `publish`,
  or an authority transfer would change its meaning.
- The generic contract carries immutable baseline coordinates but has no
  baseline list/create operation. Manager baselines therefore remain Manager
  references rather than silently projected resources.
- Current Manager canonical exports still do not carry a parity result, so they
  remain unattested until an independent validator supplies that evidence.
- Safe mutation cannot change specification artifacts; only unchanged artifact
  entries may accompany a requirement mutation.

Those gaps must be resolved in their owning contracts. This adapter must not
add automatic bidirectional sync, last-writer-wins behavior, or a private
mutation path around them.
