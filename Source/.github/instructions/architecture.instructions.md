---
applyTo: '**'
description: 'Sammlarr architecture and project-level coding constraints'
---

# Sammlarr Architecture Instructions

## Solution Architecture (authoritative)
- `UI`: Blazor Server presentation/composition root. App startup in `UI/Program.cs`.
- `API`: external-provider integrations (Plex/TMDB), HTTP clients, provider service implementations.
- `DB`: EF Core SQLite persistence, stores/repositories, DbContext + model configuration.
- `Contracts`: provider-agnostic interfaces and domain types (IDs, service contracts, enums).
- `Shared`: cross-cutting helpers (DI abstractions, options module, extensions).
- `Shared.Options.Generator`: Roslyn source generator for options registration.

## Dependency Boundaries
- Keep `Contracts` dependency-light and framework-agnostic.
- `Shared` contains reusable cross-cutting code only; do not put feature logic here.
- `DB` owns persistence concerns; do not embed EF logic in `UI` or `API`.
- `API` can depend on `DB` + `Shared` and must expose behavior through contracts.
- `UI` composes modules/services and should not implement provider protocol logic directly.

## Onion Architecture (how to apply in this solution)
- Treat `Contracts` as the core ring (domain contracts and value objects) that everything else depends on.
- Treat `API` and `DB` as outer infrastructure rings that implement behaviors defined by `Contracts`.
- Treat `UI` as the outermost composition ring (entrypoint) that wires modules and orchestrates flows.
- Dependency direction must point inward: outer rings can depend on inner rings; inner rings must not depend on outer rings.
- `Shared` is cross-cutting support for DI/options/utilities; keep it free of feature-specific business behavior.

### Onion placement rules
- Add or change domain concepts first in `Contracts` (interfaces, enums, IDs, result records).
- Implement provider protocols and external HTTP concerns in `API` only.
- Implement persistence details and EF queries in `DB` only (via stores/repositories).
- Use `UI` only to compose and orchestrate use cases via injected abstractions.
- Never call EF or provider HTTP clients directly from Razor pages/components.

## Composition and DI Patterns (required)
- Register services using module pattern (`IServiceModule`, `AddModule<TModule>()`).
- Add new service registrations in the appropriate module (`SharedModule`, `DbModule`, `ApiModule`, `UiModule`).
- Use keyed DI for provider-specific implementations:
  - Register with `AddKeyedScoped(...)`.
  - Consume via `IKeyedServiceIndex<TKey, TService>` or `[FromKeyedServices(...)]`.
- Avoid ad-hoc service locator patterns outside existing keyed abstractions.

## Provider Strategy
- Provider routing uses enums (`AuthProvider`, `MediaProvider`) + keyed services.
- For a new provider, update:
  1. Provider enum in `Contracts`.
  2. Provider contracts/implementations in `API`.
  3. Keyed DI registrations in the provider module.
  4. UI/service consumers that resolve provider-indexed services.

## Options and Configuration
- Configuration comes from `UI/appsettings.json` sections (`Sammlarr`, `Plex`, `TMDB`, connection strings).
- Define options as records implementing `IOptionsSection`.
- Do not manually duplicate options binding boilerplate; use generated `AddGeneratedOptions()` via `OptionsModule`.

## Data Access and Persistence
- Use `SammlarrDbContext` + store/repository classes in `DB/Data/Stores`.
- Prefer async EF APIs and pass `CancellationToken`.
- Keep audit timestamp behavior in `SammlarrDbContext` intact when modifying save pipelines.
- Persist provider credentials/API keys through stores (`ICredentialStore`, `IApiKeyStore`), not direct table access from UI.

## UI and External API Responsibilities
- UI components orchestrate via injected services; avoid direct HTTP calls from Razor pages.
- External API protocol concerns stay in typed clients/services (`PlexClient`, `TmdbClient`, `PlexOAuthService`, etc.).
- Keep cancellation/disposal patterns in long-running pages (`CancellationTokenSource`, dispose cleanup).

## Testing Framework and Tools
- **Test Framework**: TUnit (modern, next-gen testing framework for .NET)
- **Assertions**: AwesomeAssertions (expressive, readable assertion library)
- **Mocking**: Imposter (flexible mocking library for HTTP, databases, and other dependencies)
- Use these consistently across all test projects for unit, integration, and E2E testing.
- Write tests that are clear, maintainable, and follow existing test patterns.

## Documentation Policy
- **Do NOT create Markdown files** unless explicitly requested by the user.
- Focus on code implementation and inline documentation instead.
- If documentation is essential, confirm with user before creating any `.md` files.

## Coding Conventions to Preserve
- Follow `.editorconfig` formatting and naming.
- Use file-scoped namespaces, async/await, and nullability-aware patterns.
- Prefer existing project idioms (records, value objects via Vogen, extension members) over introducing divergent styles.
