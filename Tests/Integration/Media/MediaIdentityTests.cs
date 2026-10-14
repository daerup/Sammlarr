using System.Net;
using API.Plex;
using Contracts.Media;
using Contracts.Media.Conversions;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaIdentityTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheTmdbIdThroughPlex()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheImdbIdThroughPlex()
	{
		host.SinkAddresses(MediaProvider.Imdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Imdb("tt0137523"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenAPlexId_ResolvesTheTvdbIdThroughPlex()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Game of Thrones", Tvdb("81189"), Plex("1396")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("1396"))]).ToListAsync();

		await AssertResolved(results[0], Tvdb("81189"));
	}

	[Test]
	public async Task IdentifyAsync_ForAnImdbOnlyMovie_ResolvesTheTmdbIdThroughFind()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.TmdbFinds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523")));

		var results = await Testee.IdentifyAsync([Request(null, Imdb("tt0137523"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhilePlexIsRateLimited_ResolvesThroughTmdbWithoutAskingPlex()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Plex("550"));
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(fightClub);
		await host.TmdbFinds(fightClub);
		await host.RateLimitedAsync(RemoteService.Plex);

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"), Imdb("tt0137523"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(0);
	}

	[Test]
	public async Task IdentifyAsync_WithoutPlex_ResolvesATmdbShowToTvdbThroughTmdbsExternalIds()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.TmdbKnowsTheExternalIdsOf(new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Tvdb("81189")]));

		var results = await Testee.IdentifyAsync([new IdentifyRequest([Tmdb("1396").ToExternal(MediaType.Show)], new None())]).ToListAsync();

		await AssertResolved(results[0], Tvdb("81189"));
	}

	[Test]
	public async Task IdentifyAsync_WhenPlexCannotSupplyTheProvider_ResolvesToNothing()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenTheProviderErrors_LeavesTheIdUnmatched()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexLookupFails(Spec("Mystery", Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenATmdbIdAndTitle_ResolvesThePlexIdThroughTheMatch()
	{
		await host.PlexMatches(TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		var results = await Testee.IdentifyAsync([Request("Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
	}

	[Test]
	public async Task LoadListAsync_ForAShowNoProviderCanPlace_DescribesItsUnmatchedRowWithItsDescriptionAfterOneMatch()
	{
		await host.PlexMatchesNothing(new MediaSpec("Kusuriya no Hitorigoto 3rd Season", MediaType.Show, [Tvdb("431162")]));
		host.HttpStub.RespondWithStatusForUrl($"{TestUrls.TvmazeApi}/lookup/shows", HttpStatusCode.NotFound);
		var request = new IdentifyRequest([Tvdb("431162").ToExternal(MediaType.Show)], new None())
		{
			Description = Some.Of(
				new IdentifyHints
				{
					Title = "Kusuriya no Hitorigoto 3rd Season",
					Year = 2026,
					MediaType = MediaType.Show,
				}
			),
		};

		var results = await Testee.LoadListAsync(new[] { request }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything).InListOrderAsync().ToListAsync();

		var unmatched = await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Details.Title).IsEqualTo("Kusuriya no Hitorigoto 3rd Season");
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(1);
	}

	[Test]
	public async Task LoadListAsync_WhenATitlesIdsSitOnTwoStoredTitles_LeavesItUnmatchedAndGoesOn()
	{
		await host.SeedMediaAsync(AniListId.From("16498", MediaType.Show));
		await host.SeedMediaAsync(TvdbId.From("267440", MediaType.Show));
		var conflicting = new IdentifyRequest([AniListId.From("16498", MediaType.Show), TvdbId.From("267440", MediaType.Show)], new None());
		var other = new IdentifyRequest([Tvdb("81189").ToExternal(MediaType.Show)], new None());

		var results = await Testee
			.LoadListAsync(new[] { conflicting, other }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert.That(results).Count().IsEqualTo(2);
		await Assert
			.That(results.Count(result => result is UnmatchedMedia unmatched && unmatched.Externals.Contains(AniListId.From("16498", MediaType.Show))))
			.IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_WhenNoProviderCanPlaceTheId_ResolvesToNothing()
	{
		await host.PlexMatchesNothing(Spec("Unknown Movie", Tmdb("999")));

		var results = await Testee.IdentifyAsync([Request("Unknown Movie", Tmdb("999"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenTheIdIsAlreadyKnown_TranslatesFromCacheWithoutContactingPlex()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.Movie(TestMedia.FightClub).SeedAsync();

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenTwoIdsDescribeOneTitle_ResolveToASingleMedia()
	{
		await host.PlexMatches(TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		await Testee.IdentifyAsync([Request(null, Plex("550")), Request("Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenANewIdSharesAnExistingOne_FoldsIntoThatMedia()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.Movie(Spec("Fight Club", Tmdb("550"))).SeedAsync();
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club Plex", Tmdb("550"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
		await AssertStored(Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenTwoUnknownIdsResolveAlike_KeepsTheFirstAndLeavesTheSecondInConflict()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);
		host.PlexHolds(Spec("Fight Club Dup", Tmdb("550"), Plex("999")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550")), Request(null, Plex("999"))]).ToListAsync();

		await Assert.That(results.Count(result => result.IsSuccess())).IsEqualTo(1);
		host.ScopedDbContext.ChangeTracker.Clear();
		List<string> stored = [.. await host.ScopedDbContext.ExternalMediaIds.Select(row => row.Source + ":" + row.Id).ToListAsync()];
		await Assert.That(stored).Count().IsEqualTo(2);
		await Assert.That(stored).Contains("Tmdb:550");
		await Assert.That(stored.Count(id => id is "Plex:550" or "Plex:999")).IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_WhenAResultBridgesTwoMediaRows_LeavesThatTitleUnresolved()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.PlexMatches(Spec("Fight Club Bridge", Imdb("tt0137523"), Plex("550")));
		host.WikidataHolds("tt0137523");
		host.PlexHolds(Spec("Fight Club Bridge", Tmdb("550"), Plex("550"), Imdb("tt0137523")));

		var results = await Testee.IdentifyAsync([Request("Fight Club", Imdb("tt0137523"))]).ToListAsync();

		await Assert.That(results.Single()).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenCandidatesAreSplitAcrossRows_LeavesThatTitleUnresolved()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"), Plex("550"))]).ToListAsync();

		await Assert.That(results.Single()).IsError();
	}

	[Test]
	public async Task IdentifyAsync_WhenCandidatesAreSplitAcrossRows_NotesThePairForTheNightlyPass()
	{
		var tmdb = await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		var plex = await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();

		await Testee.IdentifyAsync([Request(null, Tmdb("550"), Plex("550"))]).ToListAsync();

		var note = (await host.Resolve<IIdentityConflicts>().AllAsync()).Single();
		MediaId[] pair = [note.Held, note.Other];
		await Assert.That(pair).IsEquivalentTo([tmdb, plex]);
	}

	[Test]
	public async Task IdentifyAsync_ForThePlexSink_StoresEveryIdPlexReportsForAPlexId()
	{
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
		await AssertStored(Plex("550"), Tmdb("550"), Imdb("tt0137523"));
	}

	[Test]
	public async Task IdentifyAsync_ForThePlexSink_FoldsAPlexIdIntoTheRowHoldingItsTmdbId()
	{
		await host.Movie(Spec("Fight Club", Tmdb("550"))).SeedAsync();
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
		await AssertStored(Plex("550"), Tmdb("550"));
		await Assert.That(await host.ScopedDbContext.Media.CountAsync()).IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_ForThePlexSinkAndAPlexIdWithoutMetadata_MissesInAFreshScopeTooAfterAskingPlexOnce()
	{
		await host.EnsurePlexAccount();
		host.PlexHasNoMetadataFor(Spec("Mystery", Plex("777")));

		var first = await Testee.IdentifyAsync([Request(null, Plex("777"))]).ToListAsync();
		await using var fresh = host.Resolve<IServiceScopeFactory>().CreateAsyncScope();
		var second = await fresh.ServiceProvider.GetRequiredService<IMediaService>().IdentifyAsync([Request(null, Plex("777"))]).ToListAsync();

		await Assert.That(first[0]).IsError();
		await Assert.That(second[0]).IsError();
		await Assert.That(host.HttpStub.Requests($"{PlexEndpoints.MetadataPath}/777")).IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_WhenPlexKnowsOnlyTheImdbId_ResolvesTheTmdbIdThroughFind()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(Spec("Fight Club", Imdb("tt0137523"), Plex("550")));
		await host.TmdbFinds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertResolved(results[0], Tmdb("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenThePlexEntryCarriesOnlyAnImdbGuid_MissesWithoutAskingByTheImdbId()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"));
		await host.TmdbKnowsTheExternalIdsOf(fightClub);
		await host.TmdbFinds(fightClub);
		await host.PlexMatchesNothing(Spec("Fight Club", Tmdb("550")));

		var results = await Testee.IdentifyAsync([Request("Fight Club", Tmdb("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_ForAnImdbIdThatPlexCannotPlace_GivesUpAfterOneMatch()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"));
		await host.TmdbFinds(fightClub);
		await host.TmdbDescribes(fightClub);
		await host.PlexMatchesNothing(fightClub);

		var results = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Imdb("tt0137523"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		await AssertRequests(match: 1, find: 0, detail: 0, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_ForTmdbImdbAndTvdbIdsThatPlexCannotPlace_GivesUpAfterOneMatch()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Tvdb("81189"));
		await host.TmdbFinds(fightClub);
		await host.TmdbDescribes(fightClub);
		await host.PlexMatchesNothing(fightClub);

		var results = await Testee
			.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"), Imdb("tt0137523"), Tvdb("81189"))])
			.ToListAsync();

		await Assert.That(results[0]).IsError();
		await AssertRequests(match: 1, find: 0, detail: 0, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_ForATvdbOnlyShow_ResolvesThePlexIdByItsTvdbGuid()
	{
		var breakingBad = new MediaSpec("Breaking Bad", MediaType.Show, [Tvdb("81189"), Plex("1396")]);
		await host.PlexMatches(breakingBad);
		host.PlexHolds(breakingBad);

		var results = await Testee.IdentifyAsync([new IdentifyRequest([Tvdb("81189").ToExternal(MediaType.Show)], new None())]).ToListAsync();

		await AssertResolved(results[0], Plex("1396"));
		await AssertRequests(match: 1, find: 0, detail: 0, detailUrl: "tv/1396?");
	}

	[Test]
	public async Task IdentifyAsync_ForATmdbIdWithoutHints_MatchesItWithoutAskingTmdb()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Plex("550"));
		await host.TmdbDescribes(fightClub);
		await host.PlexMatches(fightClub);
		host.PlexHolds(fightClub);

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
		await AssertRequests(match: 1, find: 0, detail: 0, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_WhenTmdbsDetailNamesAnImdbIdAnotherRowHolds_IdentifiesTheStoredTitleWithoutThatId()
	{
		var stored = await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		var other = await host.Movie(Spec("Fight Club (IMDb)", Imdb("tt0137523"))).SeedAsync();
		await host.TmdbKnowsTheExternalIdsOf(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523")));
		await host.PlexMatches(Spec("Fight Club", Tmdb("550"), Plex("550")));
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync();

		var identity = await Assert.That(results.Single()).IsSuccess();
		await Assert.That(identity!.Id).IsEqualTo(stored);
		await Assert.That(identity.Keys.OfProvider(MediaProvider.Imdb)).IsNone();
		var rows = await host.Resolve<IMediaRepository>().GetAsync([other]);
		await Assert.That(rows.Single().Identity.Keys.OfProvider(MediaProvider.Imdb)).IsSome();
	}

	[Test]
	public async Task IdentifyAsync_WhenTheMatchFailed_AsksAgainOnTheNextRun()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Plex("550"));
		await host.PlexMatchFailsOnceThenFinds(fightClub);
		host.PlexHolds(fightClub);

		var first = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"))]).ToListAsync();
		var second = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"))]).ToListAsync();

		await Assert.That(first[0]).IsError();
		await AssertResolved(second[0], Plex("550"));
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(2);
	}

	private static MediaSpec Spec(string title, params ProviderId[] ids) => new(title, MediaType.Movie, ids);

	private static IExternalMediaId Ext(ProviderId id) => id.ToExternal(MediaType.Movie);

	private static Optional<IdentifyHints> Hints(string? title, int year)
	{
		return Optional
			.Of(title)
			.Map(value => new IdentifyHints
			{
				Title = value,
				Year = year,
				MediaType = MediaType.Movie,
			});
	}

	private static IdentifyRequest Request(string? title, params ProviderId[] candidates) => Request(title, 0, candidates);

	private static IdentifyRequest Request(string? title, int year, params ProviderId[] candidates) => new([.. candidates.Select(Ext)], Hints(title, year));

	private async Task AssertRequests(int match, int find, int detail, string detailUrl)
	{
		using var _ = Assert.Multiple();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MatchesPath)).IsEqualTo(match);
		await Assert.That(host.HttpStub.Requests("/find/")).IsEqualTo(find);
		await Assert.That(host.HttpStub.Requests(detailUrl)).IsEqualTo(detail);
	}

	private static async Task AssertResolved(Result<MediaIdentity> result, ProviderId expected) =>
		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async identity =>
			{
				await Assert.That(identity.Keys.OfProvider(expected.Provider).Map(external => external.Value)).IsSome().And.IsEqualTo(expected.Value);
			});

	private async Task AssertStored(params ProviderId[] ids)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		foreach (var id in ids)
		{
			var exists = await host.ScopedDbContext.ExternalMediaIds.AnyAsync(entry => entry.Source == id.Provider && entry.Id == id.Value);
			await Assert.That(exists).IsTrue();
		}
	}
}
