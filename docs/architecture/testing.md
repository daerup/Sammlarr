# Testing

A map of how Sammlarr is tested and how to run each tier. The how-to-write detail lives in the
`write-tunit-test` skill; this page is the overview and the one source of truth for commands and environment variables.

Everything runs on **TUnit** over Microsoft.Testing.Platform. Test projects are executables, so the native runner is `dotnet run`, not `dotnet test` (Verify
snapshots need the native runner).

## Tiers

| Tier                      | Project                        | What belongs there                                      | Mechanism                         |
|---------------------------|--------------------------------|---------------------------------------------------------|-----------------------------------|
| Pure unit                 | `Tests/Domain`, `Tests/Shared` | Value objects, unions, algebraic laws                   | plain `[Test]`, CsCheck for laws  |
| Provider clients          | `Tests/API`                    | HTTP clients and converters                             | `TUnit.Mocks.Http` at the handler |
| Single-collaborator units | `Tests/Core`                   | A service with one dependency                           | one `IFoo.Mock()`                 |
| Behaviour + repositories  | `Tests/Integration`            | The real Core+DB+API graph, EF queries, migration drift | `SammlarrTestHost` on real SQLite |
| Components                | `Tests/UI`                     | Blazor render logic, event wiring, empty/error states   | bUnit, `.cs` only                 |
| End to end                | `Tests/E2E`                    | Cross-feature flows through a real browser              | Playwright + Aspire + WireMock    |
| Conventions               | `Tests/Architecture`           | Dependency direction, naming, design rules              | NetArchTest                       |

Rule of thumb: wiring more than one or two `.Mock()`s just to construct the subject means it belongs in `Tests/Integration` as a harness scenario, not a unit
test.

Current suite: **869** non-E2E tests plus **105** E2E.

| Domain | Shared | Core | API | DB | Architecture | Generators | Integration | UI | Web |
|--------|--------|------|-----|----|--------------|------------|-------------|----|-----|
| 49     | 153    | 102  | 118 | 21 | 21           | 2          | 314         | 76 | 13  |

## Running

```powershell
dotnet build                                             # always build first
dotnet run --project Tests/Core --no-build               # one tier
dotnet run --project Tests/E2E  --no-build               # E2E (needs Docker)
dotnet run --project Tests/Core --no-build --treenode-filter "/*/*/FilterEvaluatorTests/*"
dotnet run --project Tests/Domain --no-build -- --coverage --coverage-output-format cobertura
```

The non-E2E tiers run in roughly 20 seconds combined; E2E takes about two minutes because it boots real application stacks. Never use coverlet: it is
VSTest-only and incompatible with MTP.

## The E2E tier

Each test leases an **isolated stack** for its duration: its own Aspire application, WireMock container and SQLite database. Nothing is shared between
concurrent tests, so no test class needs
`[NotInParallel]`, and TUnit's parallel limit is the stack count. Stacks boot once per session and are pooled; a test never runs concurrently with another test
on the same stack.

How many stacks is `E2EApp.StackCount`. More is not automatically faster: each stack costs an application and a container to boot, and boot dominates a suite
this size.

Provider traffic is served by WireMock from committed fixtures under `Tests/E2E/wiremock`:
`mappings/<provider>/` holds the request matchers and `responses/<provider>/` the bodies, which are bind-mounted as WireMock's `__files`. Fixtures are committed
and `*.verified.json` is pinned to LF by `.gitattributes`.

**Replay** is the default: offline, deterministic, no secrets. **Live** (`E2E_LIVE=true`) proxies the real providers, captures what it sees, and compares
against what is committed. Each endpoint has a policy in `wiremock/fixture-policy.json`:

- `value` — the live body must match the committed fixture exactly
- `contract` — only the JSON shape is compared, so values may churn freely
- `frozen` — never written; these hold redacted placeholders and must stay that way

A drift failure writes a `.received.json` beside the fixture for review. Non-2xx provider responses are never recorded, so a rate-limited live run cannot poison
the fixture set.

Live runs genuinely mutate: they hit the real accounts. After any live run, scan the fixtures for real tokens and API keys before committing.

## Environment variables

| Variable                 | Effect                                                                         |
|--------------------------|--------------------------------------------------------------------------------|
| `E2E_LIVE=true`          | Live record mode against the real providers; needs secrets, forces one stack   |
| `E2E_HEADED=true`        | Headed browser with slow motion, for watching a test run                       |
| `E2E_PRUNE=true`         | Delete fixtures not captured during a live run; off by default, it over-prunes |
| `E2E_AUDIT=true`         | Dump served and unmatched request URLs to the temp directory                   |
| `E2E_RESOURCE_LOG=<dir>` | Write each stack's application log to `<dir>/stack-<n>.log`                    |

Live secrets come from `dotnet user-secrets` on `Tests/E2E`, named from the generated provider enums: `{PROVIDER}_API_KEY` and `{PROVIDER}_TOKEN`, for example
`TMDB_API_KEY`, `PLEX_TOKEN`.

## Mutation testing

Stryker is an occasional local audit, never a CI gate. It answers a question coverage cannot:
would a test actually fail if this line were wrong. Configs live next to the project under test and must be run from that directory, otherwise Stryker enters
solution context and runs every test project, including E2E.

```powershell
cd Domain; dotnet dotnet-stryker            # report at StrykerOutput/<timestamp>/reports/
'Domain', 'Core' | ForEach-Object { Push-Location $_; dotnet dotnet-stryker; Pop-Location }
```

It cannot run against `UI` or `Web`: Stryker bundles an older Roslyn than this SDK's Razor generator, so no `.razor` component produces a type and the projects
fail to compile inside it.

Read the `replacement` field in the report rather than the mutator name, and expect equivalent mutants. Scheduling code, deliberately redundant guards and
literal arguments like
`destructureObjects: true` often produce survivors that are not test gaps.
