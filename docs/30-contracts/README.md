---
workbench:
  type: doc
  workItems: []
  codeRefs: []
  pathHistory:
    - "C:/docs/30-contracts/README.md"
  path: /docs/30-contracts/README.md
owner: platform
status: active
updated: 2025-12-27
---

# Contracts

APIs, CLI interfaces, schemas, and external contracts.

Templates:
- `docs/templates/contract.md`

Current schemas:
- `docs/30-contracts/workbench-config.schema.json`
- `docs/30-contracts/work-item.schema.json`
- `docs/30-contracts/doc.schema.json`
- `docs/30-contracts/test-inventory.schema.json`
- `docs/30-contracts/test-run-summary.schema.json`
- `docs/30-contracts/coverage-summary.schema.json`
- `docs/30-contracts/quality-report.schema.json`

Other contract docs:
- `docs/30-contracts/cli-help.md`
- `docs/30-contracts/error-codes.md`
- `docs/30-contracts/quality-evidence-model.md`
- `docs/30-contracts/test-gate.contract.yaml`
- `docs/30-contracts/test-matrix.md`

## Guidance

- Keep schemas in sync with CLI behavior.
- `docs/30-contracts/cli-help.md` is generated from the live CLI tree.
- Regenerate it with `workbench doc regen-help`.
- Verify it is current with `workbench doc regen-help --check`.
- Machine-readable command contracts remain in `docs/commands.md`.
- Update other contract docs when inputs, outputs, or defaults change.
