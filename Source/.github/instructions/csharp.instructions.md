---
description: 'Guidelines for writing C# code in this repository'
applyTo: '**/*.cs'
---

# C# Development

## Principles and concise guidance

- Use the SDK and language level supported by the repository's `global.json` (the repository currently targets .NET SDK 10.0.103). Prefer language features that are stable in that SDK.
- Prefer the most restrictive access modifier that fits the design; widen visibility only when necessary for composition or testing.
- Prefer records/record structs for immutable value carriers and DTOs where appropriate. Use explicit properties for complex types for clarity.
- Follow naming conventions: PascalCase for public types/members, camelCase for private fields and locals, prefix interfaces with `I`.
- Follow formatting rules in `.editorconfig`.

## Comments, docs and inline guidance

- Write clear, self-documenting code. Add comments to explain non-obvious design decisions or complex logic.
- Use XML documentation (`///`) for public APIs and libraries intended for external consumption; for internal types prefer concise naming and minimal XML docs unless the API benefits from generated documentation.

## Nullable reference types

- Enable and respect nullable annotations. Use `is null`/`is not null` patterns where appropriate and validate inputs at public API boundaries.

## Data access and EF Core

- Prefer async EF Core APIs and propagate CancellationToken. Keep EF logic inside `DB` project stores/repositories.

## Testing

- Use the test framework used by the existing `Tests` projects. Keep test style consistent with surrounding tests and avoid unnecessary inline comments in tests.

## Tooling and project setup

- Follow existing project templates and structure. When creating new projects follow the solution's layering and module registration patterns.

