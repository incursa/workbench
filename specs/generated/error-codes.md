# Workbench Error Codes

Generated reference for the public process and structured error surfaces.
Keep this file aligned with the CLI error envelope and documented exit codes.

## Process Exit Codes

| Code | Meaning |
| --- | --- |
| `0` | Success. |
| `1` | Success with warnings, used by `doctor` and `validate`. |
| `2` | Failure. |

## Structured Error Envelope

| Code | Meaning |
| --- | --- |
| `repo_not_git` | The target path is not inside a git repository. |
| `access_denied` | File or repository access was denied. |
| `path_not_found` | A referenced file or directory does not exist. |
| `unexpected_error` | An unhandled error not mapped to a specific code. |

## Notes

- `validate --strict` reports findings through the validation and quality surfaces rather than via exit codes alone.
- Keep this file synchronized when CLI failure behavior changes.
