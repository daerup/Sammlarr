using System.Net;
using API.Mdblist;
using Contracts.Auth;
using Contracts.Housekeeping;
using Contracts.Media;
using Domain.Identity;
using Domain.Media;
using Domain.Providers;
using Domain.Usage;
using Harness;
using TestInfrastructure;

namespace Integration.Tests.Housekeeping;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class DuplicateTitlesTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly DateTimeOffset Now = new(2026, 10, 12, 3, 0, 0, TimeSpan.Zero);

	private IHousekeepingStep Testee => host.Step(HousekeepingStep.DuplicateTitles);

	private IIdentityConflicts Notes => host.Resolve<IIdentityConflicts>();

	[Before(Test)]
	public void SetClock() => host.SetCurrentTime(Now);

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task RunAsync_ForAPairMdblistConfirms_MergesItAndForgetsTheNote()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps(("15739", "tt0083564"));
		var (held, other) = await host.StoredTwiceAsync("15739", "tt0083564");

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(1);
		await Assert.That(await Notes.AllAsync()).IsEmpty();
		await Assert.That(await host.Resolve<IMediaRepository>().GetAsync([held, other])).HasSingleItem();
		await Assert.That(host.HttpStub.Causes(HousekeepingSeeds.MdblistTitles).Distinct()).IsEquivalentTo([UsageCause.Housekeeping]);
	}

	[Test]
	public async Task RunAsync_ForAPairMdblistMapsElsewhere_KeepsThemApartWithItsAnswer()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps(("15739", "tt0207972"));
		await host.StoredTwiceAsync("15739", "tt0083564");

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(0);
		await Assert
			.That((await Notes.AllAsync()).Single().State)
			.IsOfUnionType<ConflictState, Distinct>()
			.And.IsEqualTo(new Distinct(new AnsweredElsewhere(RemoteService.Mdblist, ImdbId.From("tt0207972", MediaType.Movie)), Now));
		await Assert.That(await Notes.DueAsync(Now.AddYears(1))).IsEmpty();
	}

	[Test]
	public async Task RunAsync_ForRowsWithIdsOfOneSource_KeepsThemApartWithoutAsking()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		var (_, other) = await host.StoredTwiceAsync("15739", "tt0083564");
		await host.Resolve<IIdentityClaims>().ClaimAsync(new IdentityClaim([TmdbId.From("196867", MediaType.Movie)], Some.Of(other), [RemoteService.Tmdb]));

		await Testee.RunAsync(Now);

		await Assert
			.That((await Notes.AllAsync()).Single().State)
			.IsOfUnionType<ConflictState, Distinct>()
			.And.IsEqualTo(new Distinct(new Contradiction(MediaProvider.Tmdb), Now));
		await Assert.That(host.HttpStub.Requests(TestUrls.MdblistApi)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_ForThreeDueNotes_AsksMdblistInOneRequest()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps(("15739", "tt0083564"), ("15740", "tt0207972"), ("15741", "tt0067992"));
		await host.StoredTwiceAsync("15739", "tt0083564");
		await host.StoredTwiceAsync("15740", "tt0207972");
		await host.StoredTwiceAsync("15741", "tt0067992");

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(3);
		await Assert.That(host.HttpStub.Requests(HousekeepingSeeds.MdblistTitles)).IsEqualTo(1);
	}

	[Test]
	public async Task RunAsync_ForAPairNoSourceKnows_AsksAgainADayThenAWeekLater()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps();
		await host.StoredTwiceAsync("15739", "tt0083564");

		await Testee.RunAsync(Now);
		var first = (await Notes.AllAsync()).Single();
		await Testee.RunAsync(first.AskAfter);
		var second = (await Notes.AllAsync()).Single();

		await Assert.That(first.AskAfter).IsEqualTo(Now.AddDays(1));
		await Assert.That(first.LastAsk).IsSome().And.IsEqualTo(new LastAsk(RemoteService.Mdblist, AskOutcome.NoMatch, Now));
		await Assert.That(second.AskAfter).IsEqualTo(Now.AddDays(8));
		await Assert.That(second.State).IsOfUnionType<ConflictState, Undecided>();
	}

	[Test]
	public async Task RunAsync_ForAPairMdblistClaimed_AsksWikidataInsteadOfTakingMdblistsWordForIt()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.WikidataMaps("tt0083564", "15739");
		var held = await host.SeedMediaAsync(ImdbId.From("tt0083564", MediaType.Movie));
		await host.SeedMediaAsync(TmdbId.From("15739", MediaType.Movie));
		await host.Resolve<IIdentityClaims>().ClaimAsync(new IdentityClaim([TmdbId.From("15739", MediaType.Movie)], Some.Of(held), [RemoteService.Mdblist]));

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(1);
		await Assert.That(host.WikidataAsked("tt0083564")).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(TestUrls.MdblistApi)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_WhenMdblistIsDown_AsksAgainADayLater()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithStatusForUrl(HousekeepingSeeds.MdblistTitles, HttpStatusCode.InternalServerError);
		await host.StoredTwiceAsync("15739", "tt0083564");

		await Testee.RunAsync(Now);

		var note = (await Notes.AllAsync()).Single();
		await Assert.That((note.Attempts, note.AskAfter)).IsEqualTo((1, Now.AddDays(1)));
		await Assert.That(note.LastAsk.Map(ask => (ask.Service, ask.Outcome))).IsSome().And.IsEqualTo((RemoteService.Mdblist, AskOutcome.Unavailable));
	}

	[Test]
	public async Task RunAsync_WhenOnlyAnotherUserHasAnMdblistKey_AsksWithThatUsersKey()
	{
		var guest = await host.SeedLocalUser("guest", "a test password", UserRole.Member);
		host.CurrentUser.Id = guest;
		await host.WithMdblistAccount("guest-mdblist-key");
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededAdmin.Id);
		host.MdblistMaps(("15739", "tt0083564"));
		await host.StoredTwiceAsync("15739", "tt0083564");

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(1);
	}

	[Test]
	public async Task RunAsync_WhileTheFirstUsersMdblistKeyIsAtItsFloor_AsksWithTheNextUsersKey()
	{
		await host.Resolve<IApiKeyService>().StoreAsync(new TestApiKey(ApiKeyProvider.Mdblist, "valid-mdblist-key"));
		AtTheFloor("valid-mdblist-key");
		var guest = await host.SeedLocalUser("guest", "a test password", UserRole.Member);
		host.CurrentUser.Id = guest;
		await host.WithMdblistAccount("guest-mdblist-key");
		host.CurrentUser.Id = UserId.From(SammlarrTestHost.SeededAdmin.Id);
		host.MdblistMaps(("15739", "tt0083564"));
		await host.StoredTwiceAsync("15739", "tt0083564");

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(HousekeepingSeeds.MdblistTitles)).IsEqualTo(1);
	}

	[Test]
	public async Task RunAsync_WhileTheOnlyMdblistKeyIsAtItsFloor_LeavesTheNoteWithoutAnAttempt()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		AtTheFloor("valid-mdblist-key");
		await host.StoredTwiceAsync("15739", "tt0083564");

		await Testee.RunAsync(Now);

		var note = (await Notes.AllAsync()).Single();
		await Assert.That((note.Attempts, note.AskAfter)).IsEqualTo((0, Now));
		await Assert.That(host.HttpStub.Requests(TestUrls.MdblistApi)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_WhenMdblistAnswersWhatItCannotRead_RecordsTheFaultAndAsksAgainADayLater()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.HttpStub.RespondWithJsonForUrl(HousekeepingSeeds.MdblistTitles, """{"titles":"not a list"}""");
		await host.StoredTwiceAsync("15739", "tt0083564");

		await Testee.RunAsync(Now);

		var note = (await Notes.AllAsync()).Single();
		await Assert.That((note.Attempts, note.AskAfter)).IsEqualTo((1, Now.AddDays(1)));
		await Assert.That(note.LastAsk.Map(ask => (ask.Service, ask.Outcome))).IsSome().And.IsEqualTo((RemoteService.Mdblist, AskOutcome.Faulted));
	}

	[Test]
	public async Task RunAsync_WhileTheUsersMdblistKeyIsAtItsFloor_AsksWikidataForTheSameUser()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		AtTheFloor("valid-mdblist-key");
		host.WikidataMaps("tt0083564", "15739");
		var held = await host.SeedMediaAsync(ImdbId.From("tt0083564", MediaType.Movie));
		await host.SeedMediaAsync(TmdbId.From("15739", MediaType.Movie));
		await host.Resolve<IIdentityClaims>().ClaimAsync(new IdentityClaim([TmdbId.From("15739", MediaType.Movie)], Some.Of(held), [RemoteService.Tmdb]));

		var merged = await Testee.RunAsync(Now);

		await Assert.That(merged).IsEqualTo(1);
		await Assert.That(host.WikidataAsked("tt0083564")).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(TestUrls.MdblistApi)).IsEqualTo(0);
	}

	[Test]
	public async Task RunAsync_Twice_MergesOnceThenReportsZero()
	{
		await host.WithMdblistAccount("valid-mdblist-key");
		host.MdblistMaps(("15739", "tt0083564"));
		await host.StoredTwiceAsync("15739", "tt0083564");

		var first = await Testee.RunAsync(Now);
		var second = await Testee.RunAsync(Now.AddDays(1));

		await Assert.That((first, second)).IsEqualTo((1, 0));
	}

	private void AtTheFloor(string key)
	{
		var budget = host.Resolve<MdblistBudget>();
		budget.Record(key, Allowance(30, Now.AddHours(-1)));
		budget.Record(key, Allowance(1000, Now.AddDays(1)));
		budget.Record(key, Allowance(400, Now.AddDays(1)));
	}

	private static HttpResponseMessage Allowance(long remaining, DateTimeOffset reset)
	{
		var answered = new HttpResponseMessage(HttpStatusCode.OK);
		answered.Headers.Add("x-ratelimit-remaining", $"{remaining}");
		answered.Headers.Add("x-ratelimit-reset", $"{reset.ToUnixTimeSeconds()}");
		return answered;
	}
}
