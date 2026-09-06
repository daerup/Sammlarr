# Split presentation from the host

| Status   | Date       |
|----------|------------|
| accepted | 2026-09-06 |

## Context

The app was one Blazor Server project that was both the UI and the composition root, so
presentation code could reference every implementation (`Core`, `DB`, `API`, `Messaging`). There
was no boundary between what the screens are and what wires the app together.

## Decision

Split into two projects. `UI` is a Razor class library holding all components and feature slices,
referencing only abstractions (`Contracts`, `Shared`, `Observability`). `Web` is the host and the
sole composition root: it holds the root components (`App`, `Routes`), `Program.cs`, the endpoints
and host services, and is the only project that references the implementations and `UI`.

Rejected: keep one project (simplest, but presentation stays coupled to every implementation);
make `AppHost` the composition root (the Aspire host is orchestration, not the application).

## Consequences

* Presentation depends on abstractions only; a component cannot reach an implementation, and the Architecture tests guard it.
* `UI` is a `Microsoft.NET.Sdk.Razor` library that references the Blazor packages directly rather than via `FrameworkReference`, because the shared-framework reference is ejected when the BlazorBlueprint components compile; the host code needing the full ASP.NET Core surface lives in `Web`.
* The root components live in the host, so the SDK treats `Web` as a Blazor Web App and serves `_framework/blazor.web.js`; `Web` registers `UI`'s assembly via `AddAdditionalAssemblies` so its routable pages are discovered.
* One more project and a host/library boundary to keep honest.
