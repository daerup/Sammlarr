using API.Plex;
using API.Tmdb;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Settings;
using Domain.Media;
using Domain.Providers;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class ProviderAnswersTests(SammlarrTestHost host) : IAsyncDisposable
{
	private static readonly MediaSpec Dune = new("Dune", MediaType.Movie, [Tmdb("438631"), Plex("438631")]);
	private static readonly MediaSpec Arrival = new("Arrival", MediaType.Movie, [Tmdb("329865"), Plex("329865")]);

	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetDetailsAsync_OnATmdbOnlyInstall_KeepsUsingWhatTmdbAnswered()
	{
		var dune = await TmdbOnlyDuneAsync();
		await Testee.GetDetailsAsync([dune]).ToListAsync();
		host.SetCurrentTime(At(6, 2));

		await Testee.GetDetailsAsync([dune]).ToListAsync();

		await AssertCached(dune, refreshed: At(6, 1), asked: At(6, 1), MediaProvider.Tmdb);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(Dune))).IsEqualTo(1);
	}

	[Test]
	public async Task GetDetailsAsync_OncePlexIsConnected_AsksOnlyPlexForWhatTmdbCouldNotGiveAndThenUsesTheCache()
	{
		var dune = await TmdbOnlyDuneAsync();
		await Testee.GetDetailsAsync([dune]).ToListAsync();
		await host.WithPlexAccount("plex-token");
		host.SetCurrentTime(At(6, 2));

		var filled = await Testee.GetDetailsAsync([dune]).ToListAsync();
		host.SetCurrentTime(At(6, 3));
		await Testee.GetDetailsAsync([dune]).ToListAsync();

		await Assert.That(await Sources(filled[0])).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb]);
		await AssertCached(dune, refreshed: At(6, 1), asked: At(6, 2), MediaProvider.Tmdb, MediaProvider.Plex);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(Dune))).IsEqualTo(1);
		await Assert.That(host.HttpStub.Requests(PlexEndpoints.MetadataPath)).IsEqualTo(1);
	}

	[Test]
	public async Task GetDetailsAsync_ForAUserWithoutATmdbKey_AsksOnlyPlex()
	{
		var arrival = await PlexOnlyArrivalAsync();

		await Testee.GetDetailsAsync([arrival]).ToListAsync();

		await AssertCached(arrival, refreshed: At(6, 1), asked: At(6, 1), MediaProvider.Plex);
		await Assert.That(host.HttpStub.Requests(TmdbDetail(Arrival))).IsEqualTo(0);
	}

	[Test]
	public async Task GetDetailsAsync_WhileTmdbIsRateLimited_UsesTheCacheAndAsksTmdbOnceTheBlockLifts()
	{
		var arrival = await PlexOnlyArrivalAsync();
		await Testee.GetDetailsAsync([arrival]).ToListAsync();
		await host.WithTmdbAccount("tmdb-key");
		host.SetCurrentTime(At(6, 1, 2));
		host.RateLimited(MediaProvider.Tmdb);

		await Testee.GetDetailsAsync([arrival]).ToListAsync();
		await AssertCached(arrival, refreshed: At(6, 1), asked: At(6, 1), MediaProvider.Plex);
		host.SetCurrentTime(At(6, 1, 2, 5));
		var filled = await Testee.GetDetailsAsync([arrival]).ToListAsync();

		await Assert.That(await Sources(filled[0])).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb]);
		await AssertCached(arrival, refreshed: At(6, 1), asked: At(6, 1, 2, 5), MediaProvider.Plex, MediaProvider.Tmdb);
	}

	[Test]
	public async Task GetDetailsAsync_WhenPlexIsDownForTheRefresh_DropsTheRatingsItLastGaveTwoWeeksAgo()
	{
		var dune = await DuneAsync();
		await StoreRefreshedOnTheFirstAsync(dune, plexAnsweredAt: At(6, 1));
		host.PlexLookupFails(Dune);
		host.SetCurrentTime(At(6, 15));

		var refreshed = await Testee.GetDetailsAsync([dune]).ToListAsync();

		await Assert.That(await Sources(refreshed[0])).IsEquivalentTo([RatingSource.Tmdb]);
		await AssertCached(dune, refreshed: At(6, 15), asked: At(6, 15), MediaProvider.Tmdb);
	}

	[Test]
	public async Task GetDetailsAsync_WhenPlexIsDownForTheRefresh_KeepsTheRatingsItGaveWithinTwoWeeks()
	{
		var dune = await DuneAsync();
		await StoreRefreshedOnTheFirstAsync(dune, plexAnsweredAt: At(6, 12));
		host.PlexLookupFails(Dune);
		host.SetCurrentTime(At(6, 15));

		var refreshed = await Testee.GetDetailsAsync([dune]).ToListAsync();

		await Assert.That(await Sources(refreshed[0])).IsEquivalentTo([RatingSource.Tmdb, RatingSource.Imdb, RatingSource.RottenTomatoesCritics]);
		await AssertCached(dune, refreshed: At(6, 15), asked: At(6, 15), MediaProvider.Tmdb, MediaProvider.Plex);
	}

	[Test]
	public async Task GetDetailsAsync_WhenEveryoneAnswersTheRefresh_DropsTheRatingsPlexNoLongerHas()
	{
		var dune = await DuneAsync();
		await StoreRefreshedOnTheFirstAsync(dune, plexAnsweredAt: At(6, 1));
		host.Movie(Dune).ServedByPlex();
		host.SetCurrentTime(At(6, 15));

		var refreshed = await Testee.GetDetailsAsync([dune]).ToListAsync();

		await Assert.That(await Sources(refreshed[0])).IsEquivalentTo([RatingSource.Tmdb]);
	}

	private async Task<MediaId> DuneAsync()
	{
		var config = host.Resolve<IProviderConfigService>();
		await config.StoreAsync((await config.GetAsync()) with { SearchProvider = SearchProvider.Tmdb });
		return await host.Movie(Dune).ServedByTmdb(rating: 7.8, votes: 12_000).ServedByPlex(imdbRating: 8.0, imdbVotes: 900_000).SeedAsync();
	}

	private async Task StoreRefreshedOnTheFirstAsync(MediaId dune, DateTimeOffset plexAnsweredAt)
	{
		var details = new MovieDetails
		{
			Title = "Dune",
			Ratings = MediaRatings.From([new TmdbRating(7.8f, Some.Of(12_000)), new ImdbRating(8.0f, Some.Of(900_000)), new RottenTomatoesCriticsRating(83)]),
		};
		ValueSet<ProviderAnswer> answers =
		[
			new(MediaProvider.Tmdb, MediaFields.None, At(6, 1)),
			new(MediaProvider.Plex, MediaFields.Rated([RatingSource.RottenTomatoesAudience]), plexAnsweredAt),
		];
		await host.Resolve<IMediaRepository>().UpsertDetailsAsync(new Dictionary<MediaId, CachedDetails> { [dune] = new(details, At(6, 1), At(6, 1), answers) });
	}

	private async Task<MediaId> TmdbOnlyDuneAsync()
	{
		var dune = await DuneAsync();
		await host.Resolve<ICredentialService>().ClearAsync(AuthProvider.Plex);
		return dune;
	}

	private async Task<MediaId> PlexOnlyArrivalAsync()
	{
		var arrival = await host.Movie(Arrival).ServedByPlex(imdbRating: 7.9, imdbVotes: 700_000).ServedByTmdb(rating: 7.6, votes: 18_000).SeedAsync();
		await host.Resolve<IApiKeyService>().ClearAsync(ApiKeyProvider.Tmdb);
		return arrival;
	}

	private async Task AssertCached(MediaId id, DateTimeOffset refreshed, DateTimeOffset asked, params MediaProvider[] answered)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		var details = await host.ScopedDbContext.MediaDetails.AsNoTracking().Include(entry => entry.Answers).SingleAsync(entry => entry.MediaId == id.Value);
		await Assert.That(details.RefreshedAt).IsEqualTo(refreshed);
		await Assert.That(details.AskedAt).IsEqualTo(asked);
		await Assert.That(details.Answers.Select(answer => answer.Provider)).IsEquivalentTo(answered);
	}

	private static async Task<IEnumerable<RatingSource>> Sources(Result<MatchedMedia> result)
	{
		var matched = await Assert.That(result).IsSuccess();
		return matched!.Details.Ratings.All.Select(rating => rating.Source);
	}

	private static string TmdbDetail(MediaSpec spec) => string.Format(TmdbEndpoints.Detail, TmdbEndpoints.MovieType, spec.Ids[0].Value);

	private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) => new(2024, month, day, hour, minute, 0, TimeSpan.Zero);
}
