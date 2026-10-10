---
name: write-tunit-test
description: Write tests in the Sammlarr repo using TUnit. Covers the tier taxonomy (unit vs the SammlarrTestHost integration harness vs bUnit UI), TUnit.Mocks / TUnit.Mocks.Http, Bogus, CsCheck property tests, Verify snapshots, the fluent union/result assertions, and how to run each. Use when adding or updating any test.
---

# Writing tests in Sammlarr

Sammlarr uses **TUnit** (not xUnit/NUnit), run on Microsoft.Testing.Platform. Get the framework basics right or the tests won't compile, and put each test in the **right tier**.

## Pick the tier first

| The unit under test is… | Tier / project | How |
|---|---|---|
| Pure domain logic, value objects, unions, an algebraic law | `Tests/Domain`, `Tests/Shared` | plain `[Test]` + fluent assertions; laws via **CsCheck** |
| A provider HTTP client / converter (Plex, TMDB, MDBList) | `Tests/API` | **TUnit.Mocks.Http** stub at the message handler |
| A service with a single collaborator | `Tests/Core` | one `IFoo.Mock()` |
| A service needing the real graph / >2 collaborators | `Tests/Integration` | **`SammlarrTestHost` full-graph mode** + thin seed helpers + a real service call |
| A repository / EF query / migration drift | `Tests/Integration` (repo tests) | `SammlarrTestHost` on real SQLite |
| A Blazor component's render logic | `Tests/UI` | **bUnit** |
| The Web host's own code (redirects, health checks, the staging seed) | `Tests/Web` | plain `[Test]`, mocks; builds Web, so needs Node |

Rule of thumb: if you're wiring up more than one or two `.Mock()`s just to construct the subject, it belongs in the harness as an integration scenario, not a unit test. Each per-layer test project (`Domain`/`Shared`/`Core`/`API`) is **pure-unit organized by feature**; `Tests/Integration` is the only home for harness-based tests, organized by feature slice (Wantlist, Media, Feeds, Identity, Settings, Notifications, Auth, …). `Tests/TestInfrastructure` and `Tests/Harness` are helper projects — never put test cases there.

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

- `[Test]` not `[Fact]`; `[Arguments(...)]` not `[InlineData]`. Every test is `async Task`; assertions are awaited.
- Unit tests: name the subject `testee` and call its real method inline (no private wrapper that re-invokes the SUT).
- Integration tests read as sentences: `A_member_cannot_list_the_users`, `A_remote_only_item_is_pulled_into_the_local_wantlist`.
- **No comments** anywhere (no XML docs, no inline notes). Qualified enum access (`MediaType.Movie`, never `using static`). Implicit lambda params. Prefer the `*Async` overload when one exists.

## Union / result assertions (`using TestInfrastructure;`)

Assert union cases with the purpose-built extensions, **never** `Assert.That(x is Case).IsTrue()` — they report the actual case on failure and narrow to the inner value for chaining.

- `Result<T>` → `.IsSuccess()` / `.IsError()`
- `Optional<T>` → `.IsSome()` / `.IsNone()`
- any union → `.IsOfUnionType<TUnion, TCase>()`; collections → `.AreOfUnionType(t1, t2, …)`

```csharp
await Assert.That(result)
    .IsSuccess()
    .And.Multiple(async user =>
    {
        await Assert.That(user.DisplayName).IsEqualTo("John");
        await Assert.That(user.Role).IsEqualTo(UserRole.Admin);
    });
```
A collection "all cases are X" check reads `values.All(v => v.IsSuccess())`. Test helpers and mock callbacks read an `Optional` or a `Result` through its helpers too, never `is Some<T>` or `is Success<T>` (`FeedRunsTests.Page`: `query.Feed.When(feed => feed == Feed).Map(…).GetValueOr([])`).

## Fake data — Bogus

```csharp
var faker = new Faker();
var title = faker.Lorem.Sentence(2);
```
Don't hardcode strings Bogus can generate. For property-style laws over generated inputs, use CsCheck (below), not Bogus.

## Unit HTTP stubbing (API tier) — TUnit.Mocks.Http

```csharp
var handler = Mock.HttpHandler();
handler.OnAnyRequest().RespondWithJson(jsonPayload);
// or: handler.OnRequest(r => r.Url.Contains("/metadata")).RespondWithJson(...)
var client = handler.CreateClient("https://api.example.com");
```
Use this whenever an API-tier unit takes an `HttpClient`. Never hit a real network. (The full-graph harness has its own edge stub — see below. Do not reach for `HttpClient` directly in either tier.)

## Mocks — TUnit.Mocks

Interfaces have generated `.Mock()` factories:
```csharp
var users = IUserRepository.Mock();
users.GetAllAsync(Any()).Returns([]);
users.FindByLoginAsync(AuthProvider.Plex, "plex-uuid", Any()).Returns(new None());
```
`Any()` / `Any<T>()` come from `TestInfrastructure`. Verify calls with `.WasCalled(Times.Once)` / `.WasNeverCalled()` — but prefer asserting real output/state over delegation checks.

**Gotcha:** when a mocked method returns `Result<T>`/`Optional<T>`, wrap the value — `.Returns(Success.Of(x))` / `.Returns(Error.From("…"))` / `.Returns(new None())`. A bare `.Returns(x)` yields a `default(Result<…>)` with **no** case set and blows up any exhaustive `switch`.

## Integration behavior — the SammlarrTestHost harness

The harness boots the real `Core`+`DB`+`API` graph on real SQLite, pins the clock, disables Polly retries, and stubs only the network edge. You seed with thin helpers, call the real service, and assert.

```csharp
[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class WantlistBehaviorTests(SammlarrTestHost host) : IAsyncDisposable
{
    private IWantlistService Testee => host.Resolve<IWantlistService>();

    public async ValueTask DisposeAsync() => await host.DisposeAsync();

    [Test]
    public async Task Adding_media_with_manual_origin_persists_the_entry()
    {
        var inception = await host.Movie(TestMedia.Inception).ServedByPlex().ServedByTmdb().SeedAsync();

        await Testee.AddAsync([inception], new WantlistItemProvenance(new ManualOrigin(ManualSource.Unknown)));

        var entries = await host.ScopedDbContext.Wantlist.AsNoTracking().ToListAsync();
        await Assert.That(entries).Count().IsEqualTo(1);
    }
}
```

Patterns:
- **Seed media** with `host.Movie(spec)` / `host.Show(spec)`, chaining `.ServedByPlex(...)`, `.ServedByTmdb(rating, votes)`, then `.SeedAsync()`, `.OnWantlist(origin)`, `.OnBlocklist()`, or `.SeedWithMetadataAsync(title, fetched)` for pre-cached metadata.
- **Stub providers** at the edge with the host helpers (`PlexSearchReturns`, `RemoteWantlistHas`, `TmdbTrendingPage`, `MdblistFeedContains`, `ProviderRejectsPushes`, …). Accounts via `EnsurePlexAccount`/`WithTmdbAccount` (chaining `ServedBy*` ensures them for you).
- **Resolve the SUT** with `host.Resolve<IService>()`; assert on real DB state via `host.ScopedDbContext`, or on emitted messages/events via `host.Published<TEvent>()`.
- **Time** is a fixed audit clock (2024-06-01); `host.SetCurrentTime(...)` moves the app-logic clock (metadata freshness), NOT the DB `CreatedAt` audit stamp — behaviors that depend on distinct persisted timestamps stay unit tests.
- **Repository tests** live in `Tests/Integration` too, using the same `[ClassDataSource<SammlarrTestHost>]` fixture against a real DbContext.
- Add a new seed helper to the harness only when a scenario needs it; keep helpers thin (seed/act/assert), never a Given/When/Then framework.

## Property tests — CsCheck

For algebraic laws over generated inputs (id round-trips, cross-provider `Equals`/`GetHashCode`, union totality, merge/reconcile invariants like "an empty load must never wipe"):

```csharp
[Test]
public void FillFrom_WhenFillingGaps_NeverRemovesData()
{
    Gen.Select(Primaries, Others).Sample((primary, other) => !primary.FillFrom(other).Missing.Overlaps(~primary.Missing));
}
```
Keep only high-value laws that catch real bugs; `.Sample(predicate)` shrinks to a minimal counter-example and reprints the seed on failure. One focused law per test, not nested `.Sample` blocks.

## Snapshot tests — Verify

For complex multi-field domain outputs where per-field asserts are noise (merged `Media`, reconcile plans, `WantlistSyncResult`):
```csharp
await Verify(merged);   // using static VerifyTUnit.Verifier;
```
Snapshots land in a `Snapshots/` folder (a `[ModuleInitializer]` sets `Verifier.DerivePathInfo`). Guids/dates auto-scrub. **Run Verify tests via `dotnet run`, not `dotnet test`** (the `.received` gotcha). `.verified.txt` is committed; `.received.*` is gitignored.

## UI components — bUnit

```csharp
public class WantlistToggleIconTests : BunitContext
{
    [Before(Test)]
    public void Setup()
    {
        Services.AddBlazorBlueprintComponents();
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Test]
    public async Task Clicking_the_icon_invokes_the_toggle_callback()
    {
        MatchedMedia? toggled = null;
        var cut = Render<WantlistToggleIcon>(p => p.Add(i => i.Result, Movie()).Add(i => i.OnToggle, v => toggled = v));

        await cut.Find("button").ClickAsync();

        await Assert.That(toggled).IsNotNull();
    }
}
```
`.cs`-only (no `.razor` test files — TUnit source-gen constraint). Test **behavior** (click → callback) and **semantics** (text, aria) — not CSS/`:hover`/scoped styles (bUnit renders no CSS; that's the E2E tier). An attribute given a `bool` (`aria-pressed="@isOn"`) renders present or absent, so assert `.HasAttribute(...)`; one given a string keeps its text, as BlazorBlueprint's radios do (`GetAttribute("aria-checked")` is `"true"` or `"false"`). Auth via `AddAuthorization().SetAuthorized("name")` / `.SetNotAuthorized()`.

## Running

TUnit apps are MTP executables. The native runner is `dotnet run`; `dotnet test` also works via the bridge but Verify needs `dotnet run`.

```powershell
dotnet run --project Tests/Core/Core.Tests.csproj --no-build            # one project
dotnet run --project Tests/Integration/Integration.Tests.csproj --no-build --treenode-filter "/*/*/SignInTests/*"   # one class
dotnet run --project Tests/Domain/Domain.Tests.csproj --no-build -- --coverage --coverage-output-format cobertura --coverage-output cov.cobertura.xml   # coverage (never coverlet)
```
Build first (`dotnet build Sammlarr.slnx`) then `--no-build`. The E2E tier (Playwright, Docker) is opt-in and excluded from the fast loop.

E2E has two pools. Most tests lease a seeded stack through `E2EApp` (`AdminAt`, `StubAsync`). A test that needs the real Radarr and Sonarr, an empty
install or Sammlarr accounts goes in `Tests/E2E/SelfHosted`, carries `[SelfHosted]` and leases through `SelfHostedApp` (`StackAsync`, `OpenPageAsync`);
`SelfHostedSetup`, `SelfHostedWantlist` and `ArrApi` hold its steps. `docs/architecture/testing.md` explains the stack, the reset and how to record.

## After writing a test file

Every file must be **analyzer-clean** (zero Rider warnings, including unused usings). Note the out-of-process analyzer emits false positives it can't see through the TUnit source generators — phantom "Cannot resolve 'Returns'/'Mock'", "cannot convert `Success<T>` to `Result<T>`", and "`using TestInfrastructure;`/`using Harness;` unused" — ignore those; the real `dotnet build` is authoritative.

## What NOT to do

- ❌ `[Fact]` / `[Theory]` / `[InlineData]` / `Assert.Equal(...)` — wrong framework.
- ❌ Real network calls — stub via `TUnit.Mocks.Http` (units) or the harness edge stub (integration).
- ❌ `Assert.That(x is Error).IsTrue()` — use `.IsError()` / `.IsNone()` / `.IsSuccess()` / `.IsSome()` / `.IsOfUnionType<…>()`.
- ❌ A bare value from a `Result`/`Optional` mock — wrap in `Success.Of`/`Error.From`/`new None()`.
- ❌ Hand-wired stateful fakes — prefer a harness scenario or a per-test `IFoo.Mock()`.
- ❌ Comments, `Moq`/`NSubstitute`, `EnsureCreated` (the harness runs the real migrations).

## ⚠️ Misleading snippet in `.github/instructions/`

`.github/instructions/dotnet-architecture-good-practices.instructions.md` shows `[Fact(DisplayName=...)]` + `Assert.Equal(...)`. **Do not copy it** — it's a generic DDD template; the repo uses TUnit. Only its naming convention applies.

See `sammlarr-dev` for project run/build commands.
