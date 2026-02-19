---
workbench:
  type: contract
  workItems: []
  codeRefs: []
  pathHistory: []
  path: /docs/30-contracts/error-codes.md
owner: platform
status: active
updated: 2026-02-19
---

# CLI Error Codes

Error envelopes are emitted for CLI failures when `--format json` is active.

## Envelope

```json
{
  "ok": false,
  "error": {
    "code": "repo_not_git",
    "message": "Target path is not inside a git repository.",
    "hint": "Run `git init` in the target directory, or pass `--repo <path>` for an existing repository."
  }
}
```

## Codes

| Code | Meaning | Typical recovery |
| --- | --- | --- |
| `repo_not_git` | Target path is not inside a git repository | initialize git or pass a valid repo path |
| `access_denied` | File system access denied | fix permissions and retry |
| `path_not_found` | Referenced file/directory missing | verify path exists |
| `unexpected_error` | Unclassified runtime failure | re-run with `--debug` and inspect diagnostics |

## Notes

- Non-JSON output mode prints friendly text and optional hint instead of stack traces.
- `--debug` (or `WORKBENCH_DEBUG=1`) includes full exception diagnostics in stderr.
