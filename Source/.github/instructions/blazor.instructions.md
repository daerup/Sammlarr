---
description: 'Blazor component and application patterns'
applyTo: '**/*.razor, **/*.razor.cs, **/*.razor.css'
---

## Blazor Code Style and Structure

- Write idiomatic and efficient Blazor and C# code.
- Follow .NET and Blazor conventions.
- Use Razor Components appropriately for component-based UI development.
- Prefer inline functions for very small components; move complex logic to code-behind files or injected services.
- Use async/await to keep UI responsive and avoid blocking the render thread.

## Naming Conventions

- Use PascalCase for component names, public types and members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., IUserService).

## Blazor and .NET Specific Guidelines

- Use the component lifecycle hooks appropriately (OnInitializedAsync, OnParametersSetAsync, etc.).
- Use `@bind` for simple two-way binding and explicit event handlers for more control.
- Prefer composition and DI: move business logic to services, keep components focused on rendering and interaction.
- Prefer language and runtime features supported by the repository's `global.json` (the SDK in this repo is 10.0.103).

## Error Handling and Validation

- Surface and log errors appropriately; use ErrorBoundary for capturing unhandled component errors.
- Validate forms using DataAnnotations or FluentValidation and provide user-friendly feedback.

## Performance and Rendering

- Minimize unnecessary renders. Avoid allocating new objects in frequently-run render paths.
- Use ShouldRender only when you have a specific rendering optimization need and document why.
- Use asynchronous APIs for long-running operations and cancel them as appropriate when components are disposed.

## Caching and State

- For Blazor Server, use scoped services and IMemoryCache for short-lived caches.
- For Blazor WebAssembly, consider localStorage/sessionStorage for persistence between reloads.
- For cross-instance caching, prefer a distributed cache (Redis, SQL backed) and document its operational requirements.

## State Management

- Use cascading parameters and EventCallback for simple state passing.
- Adopt a state library (Fluxor, BlazorState) only when the application complexity justifies it.
- For server-side session-like state, prefer scoped services and document lifetime implications.

## API Integration

- Use typed HttpClient instances (IHttpClientFactory) and implement resilient call patterns (retries, timeouts, circuit-breakers) where appropriate.
- Surface errors and provide clear UI feedback; avoid swallowing exceptions silently.

## Testing and Debugging

- Run tests with the same test framework used by the repository's `Tests` projects. Use `dotnet test` for CI-friendly runs.
- Test components using component test libraries (bUnit) or the patterns already present in repository tests.
- Use mocking libraries (Moq, NSubstitute) consistently where needed.
- For debugging client-side behavior use browser developer tools; for server-side use IDE debugging and logging.

## Security and Authentication

- Implement authentication/authorization consistently with backend APIs (JWT, cookies, or integrated identity providers as appropriate).
- Use HTTPS and configure CORS policies appropriately for the UI and API endpoints.

## Documentation

- Never create XML documentation comments or tags such as `/// <summary>` / `</summary>` in Razor components, code-behind files, or related Blazor code.
- Keep component and page README or doc comments concise — prefer small, focused non-XML documentation over large duplicative files.

<!-- End of file -->
