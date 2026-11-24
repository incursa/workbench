# ClickUp resource provider

The ClickUp provider is an out-of-process implementation of the
[`workbench.resource-provider.v1`](../../specs/requirements/WB/SPEC-WB-RESOURCE-PROVIDERS.md)
contract. It exposes ClickUp tasks as `work_item_execution` resources while
keeping canonical Workbench work items and their specification, architecture,
evidence, and provenance links under Workbench authority.

The provider never synchronizes task descriptions. Updating an execution
resource changes only the ClickUp-owned execution fields: operational status,
priority, assignees, start date, and due date. A resource delete request is
implemented as reversible ClickUp archiving; the provider never calls the
ClickUp Delete Task endpoint.

## Configuration

Build or publish `Workbench.Providers.ClickUp`, then configure its executable
through `ResourceProviders`. The provider process accepts explicit
`--name value` arguments:

| Argument | Required | Meaning |
| --- | --- | --- |
| `--list-id` | yes | ClickUp List ID and provider resource set |
| `--authority-epoch` | yes | Exact configured authority epoch used for planning and apply |
| `--mode` | no | `read-only`, `plan`, or `apply`; defaults to `read-only` |
| `--token-env` | no | Name of the inherited token environment variable; defaults to `CLICKUP_API_TOKEN` |
| `--state-directory` | plan/apply | Durable plan and idempotency-receipt directory outside the repository |
| `--local-reference-field-id` | create | Short-text ClickUp Custom Field used to reconcile create retries |
| `--api-base-uri` | no | Alternate API base URI for local mock servers; production should use the default |

For example, a published executable can be configured like this:

```json
{
  "ResourceProviders": {
    "Providers": [
      {
        "Id": "clickup",
        "Executable": "/opt/workbench/providers/Workbench.Providers.ClickUp",
        "Arguments": [
          "--list-id", "901234567890",
          "--authority-epoch", "clickup-list-901234567890-v1",
          "--mode", "apply",
          "--token-env", "CLICKUP_API_TOKEN",
          "--state-directory", "/var/lib/workbench/clickup"
        ],
        "TimeoutSeconds": 30,
        "Enabled": true
      }
    ],
    "ResourceSets": [
      {
        "ResourceType": "work_item_execution",
        "ResourceSet": "901234567890",
        "AuthorityProvider": "clickup",
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

Set the named environment variable in the process-launch environment. Do not
put a personal token or OAuth access token in repository configuration,
arguments, provider state, or logs. ClickUp requires the token in the
`Authorization` request header; see the official
[authentication guide](https://developer.clickup.com/docs/authentication).

## Modes and mutations

- `read-only` advertises query, get, validation, and health operations.
- `plan` adds read-only change planning and persists immutable, expiring plans.
- `apply` adds explicit, approved apply and durable idempotency receipts.

Planning never changes ClickUp. Apply requires the exact plan ID and digest,
authority epoch, idempotency key, and approval coordinates required by the
generic provider contract. Reusing an idempotency key for different plan
coordinates fails as a conflict. A same-key retry of a recorded apply returns
`alreadyApplied`.

Create operations require `--local-reference-field-id`. The provider stores the
stable local reference in that applicable ClickUp Custom Field and searches for
it before creating, which allows a retry to reconcile an ambiguous response
without blindly creating a duplicate. Planning verifies that the field is an
applicable text field for standard tasks, and apply re-reads the created task to
verify the marker before recording success. ClickUp documents Custom Field
behavior in its [task guide](https://developer.clickup.com/docs/tasks).
Immediately before POST, apply writes a pending-create record. If the response
is lost or invalid, the same idempotency key becomes reconciliation-only and
cannot issue a second POST while ClickUp indexing catches up.

## Deterministic mapping

| Execution resource field | ClickUp field | Behavior |
| --- | --- | --- |
| `title` | `name` | Observed on reads and required for create; never updated by reconciliation |
| `operationalStatus` | `status.status` | Exact ClickUp status name |
| `priority` | `priority.id` | `urgent=1`, `high=2`, `normal=3`, `low=4`, or `null` |
| `assigneeIds` | `assignees[].id` | String-normalized, ordinal-sorted IDs |
| `startAtUtc` | `start_date` | Unix milliseconds mapped to UTC |
| `dueAtUtc` | `due_date` | Unix milliseconds mapped to UTC |
| `archived` | `archived` | Delete plans apply `archived=true`; no remote deletion |
| `remoteTaskId` | `id` | ClickUp task identifier |
| `url` | `url` | ClickUp task URL |
| `localReference` | configured Custom Field | Stable Workbench reference, not a description marker |
| `sourceRevision` | `date_updated` | Source coordinate used in the provider version token |

Descriptions, tags, dependencies, specification links, architecture links,
evidence links, and other Workbench-authored content are deliberately outside
the remote execution resource.

## Pagination, retries, and conflicts

ClickUp list responses are limited to 100 tasks per page. The provider returns
an opaque continuation token and maintains deterministic ID ordering within a
page. See [Get Tasks](https://developer.clickup.com/reference/gettasks).

The current generic contract uses one batch limit for reads and mutations and
requires mutating providers to advertise atomic apply. Because ClickUp has no
multi-task transaction, this adapter advertises a maximum batch size of one;
the single task request is its atomic unit.

ClickUp does not expose an `ETag`/`If-Match` conditional update. The provider
therefore computes version tokens from `date_updated` plus the mapped payload
digest, re-reads the task immediately before apply, and fails closed when the
token changed. This detects stale plans but cannot eliminate the ClickUp-side
race between the final read and update. Keep one active authority process per
configured List.

Safe GET requests use bounded retries. PUT and create POST requests are never
retried inside the HTTP client after an ambiguous outcome; retry the same
approved apply and idempotency key so the provider re-reads the task and
reconciles the desired state first. HTTP 429 and transient failures return
structured, retryable diagnostics with non-secret rate-limit and `Retry-After`
coordinates. ClickUp documents the response headers in its
[rate-limit guide](https://developer.clickup.com/docs/rate-limits).

Plan files include all mutation-relevant coordinates in their digest and are
verified again before apply. A cross-process state-directory lease serializes
receipt lookup, reconciliation, mutation, and receipt persistence, preventing
two provider processes sharing that directory from applying concurrently.
Stored receipts are also revalidated against their plan, key, epoch, snapshot,
digest, and provenance coordinates before they can return `alreadyApplied`.

## Testing and operational safety

Repository tests use mocked HTTP handlers and temporary state directories;
they do not contact or mutate a live ClickUp workspace. Use `read-only` mode
when first validating configuration, then `plan` to inspect dispositions and
diagnostics before enabling explicit `apply`.

Provider stdout is reserved for the NDJSON protocol. Configuration failures go
to stderr without request headers, response bodies, task descriptions, or
credential values.
