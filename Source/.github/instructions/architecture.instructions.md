
---
applyTo: '**'
description: 'Sammlarr architecture and project-level coding guidance'
---

# Sammlarr Architecture Instructions

This document provides authoritative guidance for repository structure, dependency boundaries, and composition patterns. It is intended to help contributors place code in the correct project and follow established DI and persistence conventions.

## Solution structure (summary)
- `UI` — Blazor composition root and app startup (UI/Program.cs).
- `API` — External provider integrations, HTTP clients and provider services (Plex, TMDB, etc.).
- `DB` — EF Core persistence (SammlarrDbContext), repositories/stores and migrations.
- `Contracts` — Cross-layer DTOs, domain interfaces, IDs and enums.
- `Shared` — Cross-cutting utilities, DI helpers and options support.
- `Shared.Options.Generator` — Source generator for options registration.

## Dependency and placement rules
- Keep `Contracts` framework-agnostic and dependency-light; domain concepts belong here.
- Implement persistence only in `DB` (DbContext, queries, migrations) and avoid spreading EF concerns into `UI`/`API`.
- Provider protocol and HTTP client code belong in `API`.
- `UI` composes services and orchestrates flows; it should not implement provider protocol logic or EF queries.

## Composition & DI (recommended patterns)
- Use the established module registration pattern (e.g., modules that register services via extension methods).
- Prefer scoped lifetimes (`AddScoped`) for request-scoped services. Use singletons only when thread-safety and lifetime semantics are well understood and documented.
- For provider-specific implementations, prefer keyed registrations and explicit resolution via keyed service abstractions.

## Options & configuration
- Bind configuration via options types and the generator-backed helpers provided in the repository (avoid duplicating boilerplate).

## Data access
- Prefer asynchronous EF Core APIs and propagate CancellationToken.
- Keep persistence logic in repository/store classes under `DB` and avoid direct DbContext access in UI components.

## Testing
- Use the testing framework already used in the repository's `Tests` projects. Run tests with `dotnet test`.

## Documentation and contributor guidance
- Use Markdown files for necessary documentation. Keep content concise and maintain links from `.github/README.md` for onboarding.
- Never add XML documentation summary comments or tags such as `/// <summary>` / `</summary>` in repository source files; prefer self-documenting code and concise Markdown or non-XML comments only when needed.

## Conventions
- Follow `.editorconfig` and existing project idioms for naming, formatting and nullability.

# Installed libraries (packages present in this repository)
- Microsoft.Extensions.* (DependencyInjection, Caching.Memory, Options.ConfigurationExtensions) — core extension libraries for DI, options and caching.
- Microsoft.EntityFrameworkCore.Sqlite / InMemory (EF Core) — used for persistence and testing; EF tooling packages are available for migrations.
- Microsoft.AspNetCore.DataProtection — used for protecting sensitive data and keys.
- Vogen — value object generator used for strongly-typed IDs and value objects in Contracts/Shared.
- BlazorBlueprint.* and `timewarp-simple-icons` — UI component and icon packages used by the `UI` project.
- TUnit (TUnit.Core, TUnit.Assertions, TUnit.Mocks, TUnit.Mocks.Http) — test framework used across the Tests projects.
- Bogus and Imposter — test data generation and HTTP stubbing utilities used in tests.
- Dapper — lightweight data access helper used by some test projects.
- Microsoft.CodeAnalysis.* — Roslyn packages used by the `Shared.Options.Generator` source generator.
