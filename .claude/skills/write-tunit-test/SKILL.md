---
name: write-tunit-test
description: Write tests in the Sammlarr repo using TUnit, Bogus (fakes), Imposter (HTTP stubs), and TUnit.Mocks. Use when the user asks to add/update tests, or when adding code that needs coverage in Tests/API/, Tests/Core/, Tests/DB/, Tests/Shared/.
---

# Writing tests in Sammlarr

Sammlarr uses **TUnit** (not xUnit/NUnit). Get the framework basics right or the tests won't even compile.

## Project layout

Tests mirror the source tree: `Tests/<Project>/<Subfolder>/<Class>Tests.cs`. Add tests to the matching `Tests/<Project>/` project — don't create a new project.

- `Tests/API/` — provider clients, converters, services.
- `Tests/Core/` — business logic, services, caching.
- `Tests/DB/` — repositories (in-memory or SQLite in-memory provider).
- `Tests/Shared/` — cross-cutting utilities.
- `Tests/Test.Shared/` — **shared test helpers only** — put reusable fixtures/builders here, not test cases.

## Test syntax (TUnit)

```csharp
public class ThingTests
{
    [Test]
    public async Task Method_WhenCondition_ExpectedOutcome()
    {
        var testee = new Thing();

        var result = await testee.DoAsync();

        await Assert.That(result).IsEqualTo("expected");
    }

    [Test]
    [Arguments("movie", MediaType.Movie)]
    [Arguments("show", MediaType.Show)]
    public async Task TryParse_WhenKnownValue_ReturnsExpected(string input, MediaType expected)
    {
        var success = MediaType.TryParse(input, out var parsed);

        await Assert.That(success).IsTrue();
        await Assert.That(parsed).IsEqualTo(expected);
    }
}
```

Key points:
- `[Test]` not `[Fact]`. `[Arguments(...)]` not `[InlineData]`.
- Every test is `async Task`. Assertions are awaited: `await Assert.That(...)`.
- Name tests `Method_WhenCondition_ExpectedOutcome` (what's already in the repo).
- The subject under test is conventionally named `testee`.
- No XML doc comments on tests. No inline comments unless they explain something non-obvious.

## Assertions worth knowing

- `.IsEqualTo(x)`, `.IsTrue()`, `.IsNull()`, `.IsEmpty()`, `.Contains(x)`
- Union types: `.IsOfUnionType<TUnion, TCase>()` — returns the case for chained assertions.
- `.And.Multiple(async resolved => { ... })` to make grouped assertions on the unwrapped value.

Example from `TmdbIdConverterTests`:
```csharp
await Assert.That(result)
    .IsOfUnionType<ConversionResult, Resolved>()
    .And.Multiple(async resolved =>
    {
        await Assert.That(resolved.Id).IsEqualTo(tmdbId);
        await Assert.That(resolved.AdditionalIds).IsEmpty();
    });
```

## Fake data — Bogus

```csharp
var faker = new Faker();
var title = faker.Lorem.Sentence(2);
var year = faker.Date.Past().Year;
var id = faker.Random.Int(1, 1_000_000).ToString();
var path = faker.System.FilePath();
```
Don't hardcode strings when Bogus can generate them — the existing tests lean heavily on it.

## HTTP stubbing — Imposter

```csharp
var handler = Mock.HttpHandler();
handler.OnAnyRequest().RespondWithJson(jsonPayload);
// or: handler.OnRequest(r => r.Url.Contains("/metadata")).RespondWithJson(...)
var client = handler.CreateClient("https://api.example.com");
```
Use this whenever the unit under test takes an `HttpClient`. Don't use `HttpClient` directly against real services in tests.

## Mocks — TUnit.Mocks

Interfaces have auto-generated `Mock()` factories via `TUnit.Mocks`:
```csharp
var credentialStore = ICredentialStore.Mock();
credentialStore.GetAsync(WatchlistProvider.Plex, CancellationToken.None).Returns(credential);
```
If `IFoo.Mock()` doesn't exist, the interface hasn't been opted in — add `[Mock]` or the equivalent TUnit.Mocks attribute on the interface declaration.

## Repository tests (DB project)

Prefer **SQLite in-memory** over the EF InMemory provider — it catches relational constraints the in-memory provider silently ignores. Follow any existing pattern in `Tests/DB/` rather than inventing fixtures.

## What NOT to do

- ❌ `[Fact]`, `[Theory]`, `[InlineData]` — wrong framework.
- ❌ `Assert.Equal(expected, actual)` — old API, doesn't compile here.
- ❌ Real network calls. Always stub via Imposter.
- ❌ XML `/// <summary>` comments on tests.
- ❌ `Moq` / `NSubstitute` directly — use `TUnit.Mocks` helpers, or `Mock.HttpHandler()` for HTTP.

## ⚠️ Misleading snippet in `.github/instructions/`

`.github/instructions/dotnet-architecture-good-practices.instructions.md` shows a test template using `[Fact(DisplayName=...)]` and `Assert.Equal(expectedValue, result.Value)`. **Do not copy it.** That file is a generic DDD template; the repo actually uses TUnit. The only bit from that doc that applies to Sammlarr tests is the naming convention `MethodName_Condition_ExpectedResult()` — which matches the TUnit tests already in the repo.

## Running

```bash
dotnet test Tests/API/API.Tests.csproj
dotnet test Sammlarr.sln              # everything
```

See `sammlarr-dev` for more project commands.
