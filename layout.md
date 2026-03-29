# Recommended Layout

This file describes the repository layout used by Workbench after the Spec
Trace migration.

## Layout

```text
README.md
overview.md
layout.md
authoring.md
artifact-id-policy.json
/testdata
  README.md
  /contracts
    README.md
    doctor-non-git-error.json
    work-item.invalid-missing-id.md
    work-item.valid.md
/specs
  /requirements
    /<domain>/
      _index.md
      SPEC-<DOMAIN>[-<GROUPING>...].md
  /architecture
    /<domain>/
      <readable-file-name>.md
  /work-items
    /<domain>/
      _index.md
      <readable-file-name>.md
  /verification
    /<domain>/
      _index.md
      <readable-file-name>.md
  /generated
    commands.md
    error-codes.md
    test-matrix.md
  /templates
    spec-template.md
    architecture-template.md
    work-item-template.md
    verification-template.md
  /schemas
    artifact-frontmatter.schema.json
    artifact-id-policy.schema.json
    requirement-clause.schema.json
    requirement-trace-fields.schema.json
    work-item-trace-fields.schema.json
/quality
  attestation.yaml
  testing-intent.yaml
/artifacts
  /quality
    /attestation
    /testing
```

## Rules

- Keep the canonical source artifacts in their family roots.
- Keep generated material separate from authored artifacts.
- Keep derived attestation outputs under `artifacts/quality/attestation/`.
- Keep small machine-readable test fixtures under `testdata/`.
- Keep file names stable and readable.
- Keep generated views derived from the canonical artifacts, not the other way
  around.
