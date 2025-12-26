# Documentation

This directory contains project documentation.

## Structure

```
docs/
├── README.md          # This file
├── spec.md            # Product specification
├── commands.md        # CLI command reference
├── work-item-format.md # Work item schema
├── roadmap.md         # Feature roadmap
├── adr/               # Architecture Decision Records
│   └── 0001-record-architecture-decisions.md
├── guides/            # How-to guides and tutorials
└── api/               # API documentation (if applicable)
```

## Quick Links

- [Product Specification](./spec.md)
- [Commands Reference](./commands.md)
- [Work Item Format](./work-item-format.md)
- [Roadmap](./roadmap.md)

## Contributing

When adding documentation:

1. Use clear, concise language
2. Include examples
3. Keep documents focused on a single topic
4. Update this README when adding new documents
5. Link related documents together

## Documentation Guidelines

### Markdown Style

- Use ATX-style headers (`#`, `##`, not underlines)
- Use fenced code blocks with language tags
- Include a table of contents for long documents
- Use relative links to other docs

### Examples

All documentation should include relevant examples:
- Command-line usage examples
- Code snippets
- Configuration examples
- Expected output

### Screenshots

Include screenshots or ASCII diagrams where helpful, especially for:
- CLI output
- Complex workflows
- Visual features

## Architecture Decision Records (ADRs)

Major architectural decisions are documented as ADRs in the `/docs/adr` directory.

Template:
```markdown
# ADR-NNNN: [Title]

## Status

[Proposed | Accepted | Deprecated | Superseded]

## Context

What is the issue we're seeing that motivates this decision?

## Decision

What is the change we're proposing and/or doing?

## Consequences

What becomes easier or more difficult because of this change?
```

## Keeping Docs Up to Date

- Update docs in the same PR as code changes
- Review docs during code review
- Mark outdated docs with a warning
- Remove deprecated docs after 2 versions
