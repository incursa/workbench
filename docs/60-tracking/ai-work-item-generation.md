---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  changeNotes: []
  pathHistory:
    - "C:/docs/60-tracking/ai-work-item-generation.md"
  path: /docs/60-tracking/ai-work-item-generation.md
owner: platform
status: draft
updated: 2025-12-27
---

# AI work item generation

Workbench can draft work items from a freeform description using an AI model.
The `workbench item generate` command asks the model to return a title, summary,
and acceptance criteria, then creates the work item file from the templates.

## Behavior

- Requires `WORKBENCH_AI_PROVIDER` and API credentials (same as doc summaries).
- Uses a dedicated model override for work item generation when provided.
- Creates a standard work item file under `docs/70-work/items` using the selected type.

## Configuration

Required:

- `WORKBENCH_AI_OPENAI_KEY` or `OPENAI_API_KEY`
- `WORKBENCH_AI_PROVIDER` (`openai` or `none`; default: `openai`)

Optional:

- `WORKBENCH_AI_WORK_ITEM_MODEL` (overrides the model for item generation)
- `WORKBENCH_AI_WORK_ITEM_INSTRUCTIONS` (override the default prompt)
- `WORKBENCH_AI_MODEL` or `OPENAI_MODEL` (fallback model if no work item override is set)

## Example

```bash
workbench item generate --prompt "Investigate flaky login tests when run in CI"
```
