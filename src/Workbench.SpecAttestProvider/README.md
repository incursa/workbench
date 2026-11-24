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

The current Manager contract does not expose an exact Standard pin and parity
receipt on canonical exports. Exported snapshots are therefore marked
`canonicalProfilePassed=false`; they are inspectable but cannot be accepted as
an authority transition.

The current Manager canonical import writes artifacts and requirements through
separate operations, while its atomic administrative project import cannot
safely reconstruct Manager-owned audit/provenance from portable canonical
content. The provider consequently advertises only `readOnlyEnricher` and does
not advertise `planChanges` or `applyChanges`. It fails closed if those methods
are invoked. Do not configure it as the authority or publication target until
Manager provides a pin-aware, staged, atomic, optimistic-concurrency-protected,
idempotent interchange apply API.

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
  "canonicalization": "utf8-json-sorted-object-keys-v1",
  "profile": "core"
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
- Current Manager exports do not carry the exact Standard tuple, parity result,
  or authority manifest required for acceptance.
- Current Manager import/publication endpoints do not jointly provide the
  staged full-set transaction, optimistic concurrency, idempotency,
  provenance, and exact-pin checks required by `SPEC-001`.

Those gaps must be resolved in their owning contracts. This adapter must not
add automatic bidirectional sync, last-writer-wins behavior, or a private
mutation path around them.
