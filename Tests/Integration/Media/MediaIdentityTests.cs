using System.Net;
using API.Plex;
using Contracts.Caching;
using Contracts.Media;
using Contracts.Media.Conversions;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
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
		host.RateLimited(RemoteService.Plex);

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
	public async Task IdentifyAsync_WhenResolvingAPlexId_StoresEveryIdPlexReported()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);

		await Testee.IdentifyAsync([Request(null, Plex("550"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenGivenATmdbIdAndTitle_ResolvesThePlexIdThroughSearch()
	{
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
		host.PlexHolds(TestMedia.FightClub);

		var results = await Testee.IdentifyAsync([Request("Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
	}

	[Test]
	public async Task IdentifyWithDetailsAsync_ForAShowNoProviderCanPlace_DescribesItsUnmatchedRowWithItsDescriptionWithoutSearchingByIt()
	{
		await host.EnsurePlexAccount();
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

		var results = await Testee.IdentifyWithDetailsAsync(new[] { request }.ToAsyncEnumerable()).ToListAsync();

		var unmatched = await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Details.Title).IsEqualTo("Kusuriya no Hitorigoto 3rd Season");
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(0);
	}

	[Test]
	public async Task IdentifyAsync_WhenNoProviderCanPlaceTheId_ResolvesToNothing()
	{
		await host.PlexFindsNothing("Unknown Movie");

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
		await host.PlexSearchReturns("Fight Club", TestMedia.FightClub);
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
	public async Task IdentifyAsync_WhenTwoUnknownIdsResolveAlike_CollapseIntoOneMedia()
	{
		host.SinkAddresses(MediaProvider.Tmdb);
		await host.EnsurePlexAccount();
		host.PlexHolds(TestMedia.FightClub);
		host.PlexHolds(Spec("Fight Club Dup", Tmdb("550"), Plex("999")));

		await Testee.IdentifyAsync([Request(null, Plex("550")), Request(null, Plex("999"))]).ToListAsync();

		await AssertStored(Tmdb("550"), Plex("550"), Plex("999"));
	}

	[Test]
	public async Task IdentifyAsync_WhenAResultBridgesTwoMediaRows_IsRejectedAsAConflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club (Plex)", Plex("550")));
		host.PlexHolds(Spec("Fight Club Bridge", Tmdb("550"), Plex("550"), Imdb("tt0137523")));

		await Assert
			.That(async () => await Testee.IdentifyAsync([Request("Fight Club", Imdb("tt0137523"))]).ToListAsync())
			.Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task IdentifyAsync_WhenCandidatesAreSplitAcrossRows_AreRejectedAsAConflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (Plex)", Plex("550"))).SeedAsync();

		await Assert.That(async () => await Testee.IdentifyAsync([Request(null, Tmdb("550"), Plex("550"))]).ToListAsync()).Throws<MediaIdentityConflictException>();
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
	public async Task IdentifyAsync_ForThePlexSink_CachesAPlexIdWithoutMetadataAsUnmatched()
	{
		await host.EnsurePlexAccount();
		host.PlexHasNoMetadataFor(Spec("Mystery", Plex("777")));

		var results = await Testee.IdentifyAsync([Request(null, Plex("777"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		var unmatched = host.Resolve<ITypedMemoryCache<string, bool>>();
		var connected = await host.Resolve<MediaIdResolver>().ConnectedAsync(CancellationToken.None);
		await Assert.That(unmatched.GetValueOrNone(UnmatchedCacheKey.Build([Ext(Plex("777"))], MediaProvider.Plex, connected))).IsSome();
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
	public async Task IdentifyAsync_WhenThePlexEntryCarriesOnlyAnImdbGuid_ResolvesThePlexIdThroughTheImdbId()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"));
		await host.TmdbKnowsTheExternalIdsOf(fightClub);
		await host.TmdbFinds(fightClub);
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club", Plex("550")));
		host.PlexHolds(Spec("Fight Club", Imdb("tt0137523"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request("Fight Club", Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_ForAnImdbIdThatPlexCannotPlace_GivesUpAfterEveryRoute()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"));
		await host.TmdbFinds(fightClub);
		await host.TmdbDescribes(fightClub);
		await host.PlexFindsNothing("Fight Club");

		var results = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Imdb("tt0137523"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		await AssertRequests(search: 1, find: 1, detail: 1, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_ForTmdbImdbAndTvdbIdsThatPlexCannotPlace_GivesUpAfterEveryRoute()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"), Tvdb("81189"));
		await host.TmdbFinds(fightClub);
		await host.TmdbDescribes(fightClub);
		await host.PlexFindsNothing("Fight Club");

		var results = await Testee
			.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"), Imdb("tt0137523"), Tvdb("81189"))])
			.ToListAsync();

		await Assert.That(results[0]).IsError();
		await AssertRequests(search: 1, find: 2, detail: 1, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_ForATvdbOnlyShow_ResolvesThePlexIdThroughTmdb()
	{
		var breakingBad = new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Tvdb("81189"), Plex("1396")]);
		await host.TmdbFinds(breakingBad);
		await host.TmdbDescribes(breakingBad);
		await host.PlexSearchReturns("Breaking Bad", breakingBad);
		host.PlexHolds(breakingBad);

		var results = await Testee.IdentifyAsync([new IdentifyRequest([Tvdb("81189").ToExternal(MediaType.Show)], new None())]).ToListAsync();

		await AssertResolved(results[0], Plex("1396"));
		await AssertRequests(search: 1, find: 1, detail: 0, detailUrl: "tv/1396?");
	}

	[Test]
	public async Task IdentifyAsync_ForATmdbIdWithoutHints_SearchesPlexForTmdbsOriginalTitle()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Plex("550"));
		await host.TmdbDescribes(fightClub);
		await host.PlexSearchReturns("Fight Club", fightClub);
		host.PlexHolds(fightClub);

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
	}

	[Test]
	public async Task IdentifyAsync_WhenPlexFindsNothingForTmdbsOriginalTitle_ResolvesToNothing()
	{
		await host.TmdbDescribes(Spec("Fight Club", Tmdb("550")));
		await host.PlexFindsNothing("Fight Club");

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
	}

	[Test]
	public async Task IdentifyAsync_ForATmdbIdWhileTmdbIsUnavailable_LeavesTheTitleForTheNextRun()
	{
		await host.EnsurePlexAccount();
		await host.EnsureTmdbAccount();
		host.TmdbDetailFails(Spec("Fight Club", Tmdb("550")), HttpStatusCode.ServiceUnavailable);

		var results = await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		var unmatched = host.Resolve<ITypedMemoryCache<string, bool>>();
		var connected = await host.Resolve<MediaIdResolver>().ConnectedAsync(CancellationToken.None);
		await Assert.That(unmatched.GetValueOrNone(UnmatchedCacheKey.Build([Ext(Tmdb("550"))], MediaProvider.Plex, connected))).IsNone();
	}

	[Test]
	public async Task IdentifyAsync_WhenTmdbsDetailNamesAnImdbIdAnotherRowHolds_IsRejectedAsAConflict()
	{
		await host.Movie(Spec("Fight Club (TMDB)", Tmdb("550"))).SeedAsync();
		await host.Movie(Spec("Fight Club (IMDb)", Imdb("tt0137523"))).SeedAsync();
		await host.TmdbKnowsTheExternalIdsOf(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523")));
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club", Tmdb("550"), Plex("550")));
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Plex("550")));

		await Assert.That(async () => await Testee.IdentifyAsync([Request(null, Tmdb("550"))]).ToListAsync()).Throws<MediaIdentityConflictException>();
	}

	[Test]
	public async Task IdentifyAsync_WhenThePlexHitCarriesOnlyATmdbGuid_ConfirmsItThroughTheTmdbIdOfTheImdbId()
	{
		await host.TmdbFinds(Spec("Fight Club", Tmdb("550"), Imdb("tt0137523")));
		await host.PlexSearchReturns("Fight Club", Spec("Fight Club", Plex("550")));
		host.PlexHolds(Spec("Fight Club", Tmdb("550"), Plex("550")));

		var results = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Imdb("tt0137523"))]).ToListAsync();

		await AssertResolved(results[0], Plex("550"));
		await AssertRequests(search: 1, find: 1, detail: 0, detailUrl: "movie/550?");
	}

	[Test]
	public async Task IdentifyAsync_WhenTmdbsHintsMatchTheFeeds_SearchesPlexOnce()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Imdb("tt0137523"));
		await host.TmdbKnowsTheExternalIdsOf(fightClub);
		await host.TmdbFinds(fightClub);
		await host.PlexFindsNothing("Fight Club");

		var results = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(1);
	}

	[Test]
	public async Task IdentifyAsync_WhenThePlexSearchFailed_SearchesAgainOnTheNextRun()
	{
		var fightClub = Spec("Fight Club", Tmdb("550"), Plex("550"));
		await host.PlexSearchFailsOnceThenReturns("Fight Club", fightClub);
		host.PlexHolds(fightClub);

		var first = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"))]).ToListAsync();
		var second = await Testee.IdentifyAsync([Request("Fight Club", SammlarrTestHost.DefaultYear, Tmdb("550"))]).ToListAsync();

		await Assert.That(first[0]).IsError();
		await AssertResolved(second[0], Plex("550"));
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(2);
	}

	[Test]
	public async Task IdentifyAsync_WhenTmdbsOriginalTitleDiffers_SearchesPlexForItToo()
	{
		await host.TmdbDescribes(Spec("Sen to Chihiro no Kamikakushi", Tmdb("129")));
		await host.PlexFindsNothing("Spirited Away");
		await host.PlexFindsNothing("Sen to Chihiro no Kamikakushi");

		var results = await Testee.IdentifyAsync([Request("Spirited Away", SammlarrTestHost.DefaultYear, Tmdb("129"))]).ToListAsync();

		await Assert.That(results[0]).IsError();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(2);
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

	private async Task AssertRequests(int search, int find, int detail, string detailUrl)
	{
		using var _ = Assert.Multiple();
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.SearchPath)).IsEqualTo(search);
		await Assert.That(host.HttpStub.Requests("/find/")).IsEqualTo(find);
		await Assert.That(host.HttpStub.Requests(detailUrl)).IsEqualTo(detail);
	}

	private static async Task AssertResolved(Result<MediaIdentity> result, ProviderId expected) =>
		await Assert
			.That(result)
			.IsSuccess()
			.And.Multiple(async identity =>
			{
				await Assert.That(identity.Externals.OfProvider(expected.Provider).Map(external => external.Value)).IsSome().And.IsEqualTo(expected.Value);
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
