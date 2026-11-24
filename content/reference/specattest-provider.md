# SpecAttest resource provider

The first-party SpecAttest provider is an out-of-process adapter from the
[`workbench.resource-provider.v1`](../../specs/requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
contract to one local or hosted SpecAttest Manager project. It preserves the
accepted declared-authority model: SpecAttest Standard owns the language, each
specification set has one live writer, and imports, proposals, publications,
and authority transfers remain explicit transitions.

## Supported read and interchange surface

The provider currently supports:

- local loopback Manager endpoints over HTTP or HTTPS;
- hosted Manager endpoints over HTTPS with a bearer token inherited from the
  configured environment variable;
- one explicit specification-set UUID mapped to one Manager project;
- an exact caller-supplied `specattest.standard-pin.v1` tuple;
- deterministic translation from Manager canonical export to
  `specattest.interchange.v1`;
- a JSON manifest plus deterministic ZIP payload containing canonical
  artifacts, requirements, retired-ID ledger, evidence descriptors, external
  references, entry manifest, and audit references;
- lossless canonical JSON and `x_` extension round-trip;
- fail-closed validation of authority mode, immutable source coordinates,
  Standard pin, stable IDs, retired IDs, scope, and every payload digest.

Manager's current canonical export does not contain its exact Standard pin and
cross-tool parity receipt. The adapter therefore marks translated exports
`canonicalProfilePassed=false`. They are inspectable store-and-forward
snapshots, not acceptable authority transitions or publication inputs.

## Process configuration

Build or publish `Workbench.SpecAttestProvider`, then configure its
`workbench-specattest-provider` executable through `ResourceProviders`.

| Argument | Required | Meaning |
| --- | --- | --- |
| `--mode` | yes | `local` for a loopback endpoint or `hosted` for an HTTPS endpoint |
| `--endpoint` | yes | Manager service root without credentials, query, or fragment |
| `--set-id` | yes | Immutable specification-set UUID |
| `--project-id` | yes | Exact Manager project identifier |
| `--authority-mode` | yes | `git-workbench` or `manager` |
| `--authority-provider-id` | yes | Exact configured provider ID of the declared live authority |
| `--authority-epoch` | yes | Exact declared authority epoch |
| `--standard-pin-file` | yes | JSON file containing the complete approved Standard tuple |
| `--token-env` | no | Token environment-variable name; defaults to `WORKBENCH_SPECATTEST_TOKEN` |
| `--provider-id` | no | Stable provider ID; defaults to `specattest` |

The current provider advertises only `readOnlyEnricher`. For a Git-canonical
set, configure another provider as authority and place SpecAttest in
`ReadOnlyEnrichers`:

```json
{
  "ResourceProviders": {
    "Providers": [
      {
        "Id": "git",
        "Executable": "<configured-git-authority-provider>",
        "Arguments": [],
        "Enabled": true
      },
      {
        "Id": "specattest-review",
        "Executable": "/opt/workbench/providers/workbench-specattest-provider",
        "Arguments": [
          "--mode", "hosted",
          "--endpoint", "https://manager.example.com/",
          "--set-id", "018f5f6e-f5d9-7a55-8f0f-63b518fb19a4",
          "--project-id", "manager-project-1",
          "--authority-mode", "git-workbench",
          "--authority-provider-id", "git",
          "--authority-epoch", "git-epoch-7",
          "--standard-pin-file", ".workbench/specattest-standard-pin.json",
          "--token-env", "WORKBENCH_SPECATTEST_TOKEN",
          "--provider-id", "specattest-review"
        ],
        "Enabled": true
      }
    ],
    "ResourceSets": [
      {
        "ResourceType": "specattest_set",
        "ResourceSet": "018f5f6e-f5d9-7a55-8f0f-63b518fb19a4",
        "AuthorityProvider": "git",
        "ReadOnlyEnrichers": ["specattest-review"],
        "PublicationTargets": [],
        "ConflictPolicy": "fail",
        "OfflinePolicy": "store-and-forward"
      }
    ],
    "DefaultTimeoutSeconds": 30
  }
}
```

The pin file has no default and must never use `main` or `latest`:

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

Never place a bearer token in repository configuration, process arguments,
provider output, or store-and-forward packages.

## Fail-closed mutation boundary

The provider does not advertise `planChanges` or `applyChanges`. Manager's
current canonical import writes artifacts and requirements through separate
operations. Its atomic administrative project import cannot reconstruct
Manager-owned audit and provenance safely from portable canonical content.
Neither path supplies the exact-pin, staged full-set transaction,
optimistic-concurrency, idempotency, and provenance guarantees required for an
authority change.

Workbench's generic v1 protocol also has no operation for staging a
non-authoritative snapshot or for listing/creating baselines. Treating those
transitions as `create`, `publish`, or authority transfer would change their
meaning. The adapter reports these as upstream contract gaps instead of
inventing private CRUD or synchronization behavior.

Until the owning contracts close those gaps:

- do not configure this provider as authority or publication target;
- do not infer authority transfer from export, import, endpoint availability,
  or timestamps;
- do not automatically merge or bidirectionally synchronize normative JSON;
- keep Manager baselines and publications as immutable external coordinates;
- require a new, exact, validated package after any source or pin change.

The implementation-specific argument parser and model notes are also available
in [`src/Workbench.SpecAttestProvider/README.md`](../../src/Workbench.SpecAttestProvider/README.md).
