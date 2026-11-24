# CSV resource provider

The first-party CSV provider is a fully local, out-of-process implementation
of the
[`workbench.resource-provider.v1`](../../specs/requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
contract for `work_item` and `specification` resource sets. It writes no
network data and uses the same authority, optimistic-concurrency, planning,
approval, provenance, and idempotency rules as every other provider.

The provider has two deliberately different modes:

- `authority` owns normative CSV files and accepts mutation only through an
  immutable preview plan followed by explicit apply.
- `projection` reads an editable CSV projection when another provider remains
  authoritative. It never applies normative changes itself; reconciliation
  proposals are sent to the configured authority for its own plan/apply flow.

## Files and version coordinates

One provider root contains these versioned files:

| Resource type | File | Row schema coordinate |
| --- | --- | --- |
| `work_item` | `work-items.v1.csv` | `workbench.csv.work-item.v1` |
| `specification` | `specifications.v1.csv` | `workbench.csv.specification.v1` |

The normalized schemas are
[`csv-work-item-row-v1.schema.json`](../../specs/schemas/csv-work-item-row-v1.schema.json)
and
[`csv-specification-row-v1.schema.json`](../../specs/schemas/csv-specification-row-v1.schema.json).
Copy-ready headers are available in
[`csv-work-items-v1.csv`](../../specs/templates/csv-work-items-v1.csv) and
[`csv-specifications-v1.csv`](../../specs/templates/csv-specifications-v1.csv).

Every row carries the resource/provider/schema coordinates, authority epoch,
base version and digest, and provenance needed to detect stale edits. The
provider rejects unknown schema versions, duplicate IDs or headers, malformed
rows, and authoritative rows whose content was edited outside plan/apply.
Additional columns are retained as string-valued `csvExtensions`; an
extension that cannot be represented as a CSV string is explicitly rejected.

Provider output uses UTF-8 with a BOM, comma delimiters, CRLF records, and
RFC 4180-compatible quoting. Input accepts UTF-8 with or without a BOM and LF
or CRLF records. Commas, doubled quotes, Unicode, and embedded newlines are
round-tripped. This is the common interchange subset used by current Excel,
Numbers, and LibreOffice CSV import/export; choose comma and UTF-8 explicitly
when an application's locale proposes another delimiter or encoding.

## Specification rows are atomic

Each `specification` row is one normative requirement, not an entire
multi-requirement document. Its `statement` must contain exactly one approved
uppercase keyword: `MUST`, `MUST NOT`, `SHALL`, `SHALL NOT`, `SHOULD`,
`SHOULD NOT`, or `MAY`. The `normative_keyword` column must match the keyword
in the statement. Multiple sentence endings produce a warning because one
obligation should normally be one sentence; zero or multiple approved
keywords are invalid.

## Provider process configuration

Build or publish `Workbench.CsvProvider`, then configure the resulting
`workbench-csv-provider` executable. It accepts explicit `--name value`
arguments:

| Argument | Required | Meaning |
| --- | --- | --- |
| `--root` | yes | Local directory containing the versioned CSV and provider state |
| `--resource-set` | yes | Exact provider resource-set identifier |
| `--authority-epoch` | yes | Exact epoch used for plan/apply concurrency |
| `--role` | yes | `authority` or `projection` |
| `--provider-id` | no | Stable provider ID; defaults to `workbench-csv` |
| `--provider-version` | no | Provider build/version coordinate; defaults to `1.0.0` |
| `--authority-provider-id` | projection | Required external authority ID retained in projection rows; it must differ from the projection provider ID |

An authoritative work-item set can be configured as follows:

```json
{
  "ResourceProviders": {
    "Providers": [
      {
        "Id": "csv-roadmap",
        "Executable": "/opt/workbench/providers/workbench-csv-provider",
        "Arguments": [
          "--root", "data/roadmap-csv",
          "--resource-set", "roadmap",
          "--authority-epoch", "roadmap-csv-v1",
          "--role", "authority",
          "--provider-id", "csv-roadmap"
        ],
        "Enabled": true
      }
    ],
    "ResourceSets": [
      {
        "ResourceType": "work_item",
        "ResourceSet": "roadmap",
        "AuthorityProvider": "csv-roadmap",
        "ReadOnlyEnrichers": [],
        "PublicationTargets": [],
        "ConflictPolicy": "fail",
        "OfflinePolicy": "store-and-forward"
      }
    ],
    "DefaultTimeoutSeconds": 30
  }
}
```

The process reserves stdout for protocol messages. The `.workbench-csv-provider`
directory under `--root` contains immutable plans, apply receipts, and the
cross-process apply lock; do not edit it manually.

## Authoritative workflow

1. Query/get the current resources and retain their version tokens.
2. Validate the proposed snapshot or content.
3. Call `planChanges` with the current authority epoch, content digests, and
   expected version tokens for updates/deletes.
4. Review every disposition and diagnostic. Planning does not touch CSV.
5. Obtain explicit approval and call `applyChanges` with the exact plan ID,
   plan digest, authority epoch, approval, and a stable idempotency key.
6. Re-query after apply. A same-key/same-plan retry returns `alreadyApplied`;
   a changed file, stale token/epoch, or reused key with other coordinates
   returns a conflict.

Delete is supported only as an explicit planned `delete` change. No missing
row is ever interpreted as deletion.

## Editable projection and reconciliation

Export an immutable authority snapshot with `CsvProjectionReconciler.Export`,
open the resulting CSV in a spreadsheet, and set `operation` only on rows that
are intended to change:

The reconciler defaults its projection identity to
`workbench-csv-projection`. If the authority already uses that ID, pass a
different projection provider ID explicitly; projection and authority IDs must
never be equal.

- `none`: no proposal; edited content is reported but ignored.
- `upsert`: propose create or update after base-coordinate validation.
- `delete`: propose deletion of the named baseline resource.

Removing a row from the projection does not propose deletion. After saving,
use `CsvProjectionReconciler.Preview` and review its complete change list and
diagnostics. `PlanAsync` sends only a conflict-free preview to the actual
authority, and `ApplyAsync` still requires a reviewed authority plan, explicit
approval, and idempotency key. Re-export after a successful apply; the
projection is never silently rewritten or promoted to authority.

```csharp
var csvPath = CsvProjectionReconciler.Export(authoritySnapshot, "data/review");
// Edit csvPath in Excel, Numbers, or LibreOffice and set operation explicitly.
var preview = CsvProjectionReconciler.Preview(authoritySnapshot, "data/review");
var plan = await CsvProjectionReconciler.PlanAsync(authorityClient, preview);
var result = await CsvProjectionReconciler.ApplyAsync(
    authorityClient,
    plan,
    authoritySnapshot.ResourceType,
    authoritySnapshot.ResourceSet,
    "review-2026-11-22-001",
    new ProviderApproval("reviewer", "change-review:42", DateTimeOffset.UtcNow));
```

The caller must keep the baseline snapshot immutable until preview is
complete. If the authority changes, stale base coordinates conflict and the
projection must be re-exported before another attempt.
