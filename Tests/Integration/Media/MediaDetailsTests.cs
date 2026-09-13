using Contracts.Media;
using Contracts.Media.Search;
using Domain.Media;
using Domain.Providers;
using Harness;
using Integration.Tests.Assertions;
using Microsoft.EntityFrameworkCore;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaDetailsTests(SammlarrTestHost host) : IAsyncDisposable
{
	private IMediaService Testee => host.Resolve<IMediaService>();

	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task GetDetailsAsync_WhenTheCacheIsFresh_ServesItWithoutContactingProviders()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(5, 31));

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Cached Title");
	}

	[Test]
	public async Task GetDetailsAsync_WhenTheCacheIsStale_RefetchesAndStoresIt()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(5, 24));

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Live Title");
		await AssertCachedAs(fightClub, "Live Title", At(6, 1));
	}

	[Test]
	public async Task GetDetailsAsync_WhenTheProviderIsUnreachable_ServesTheStaleCache()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(5, 24));
		host.PlexLookupFails(TestMedia.FightClub);

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Cached Title");
		await AssertCachedAs(fightClub, "Cached Title", At(5, 24));
	}

	[Test]
	public async Task GetDetailsAsync_WhenTheMediaIsUncached_FetchesFromProvidersAndStoresIt()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex(title: "").ServedByTmdb(title: "Live Title").SeedAsync();

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Live Title");
		await AssertCachedAs(fightClub, "Live Title", At(6, 1));
	}

	[Test]
	public async Task GetDetailsAsync_WhenIncompleteWithinTheCooldown_ServesFromCache()
	{
		host.SetCurrentTime(At(6, 1, 11, 30));
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(6, 1, 11, 0), genres: []);

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Cached Title");
		await AssertCachedAs(fightClub, "Cached Title", At(6, 1, 11, 0));
	}

	[Test]
	public async Task GetDetailsAsync_WhenIncompletePastTheCooldown_RefetchesIt()
	{
		host.SetCurrentTime(At(6, 1, 11, 30));
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(6, 1, 9, 0), genres: []);

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Live Title");
		await AssertCachedAs(fightClub, "Live Title", At(6, 1, 11, 30));
	}

	[Test]
	public async Task GetDetailsAsync_WhenThePosterIsEmptyPastTheCooldown_RefetchesIt()
	{
		host.SetCurrentTime(At(6, 1, 11, 30));
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(6, 1, 9, 0), poster: "");

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Live Title");
	}

	[Test]
	public async Task GetDetailsAsync_WhenBatched_ResolvesEachItemByItsOwnCacheState()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Fresh Title", fetched: At(6, 1));
		var matrix = await host.Movie(Spec("The Matrix", Tmdb("603"), Plex("603")))
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Matrix")
			.SeedWithMetadataAsync(title: "Old Matrix", fetched: At(5, 24));

		var results = await Testee.GetDetailsAsync([fightClub, matrix]).ToListAsync();

		await AssertTitle(results[0], "Fresh Title");
		await AssertTitle(results[1], "Live Matrix");
	}

	[Test]
	public async Task GetDetailsAsync_WhenUncached_EnrichesAcrossTheChainAndCachesTheMerge()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club", summary: "An insomniac office worker")
			.ServedByTmdb(title: "TMDB Fight Club", summary: "TMDB plot synopsis", genres: ["Drama", "Thriller"], rating: 8.4, votes: 23000)
			.SeedAsync();

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "An insomniac office worker", genres: ["Drama", "Thriller"], rating: RatingProvider.Tmdb);
		await AssertDetailCachedAs(Detail(results[0]), fightClub, At(6, 1));
	}

	[Test]
	public async Task GetDetailsAsync_WhenStale_ReenrichesAndReplacesTheDetail()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club", summary: "An insomniac office worker")
			.ServedByTmdb(title: "TMDB Fight Club", summary: "TMDB plot synopsis", genres: ["Drama", "Thriller"], rating: 8.4, votes: 23000)
			.SeedWithMetadataAsync(title: "Stale Title", summary: "Stale summary", genres: ["Stale"], fetched: At(5, 24));

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "An insomniac office worker", genres: ["Drama", "Thriller"], rating: RatingProvider.Tmdb);
		await AssertDetailCachedAs(Detail(results[0]), fightClub, At(6, 1));
	}

	[Test]
	public async Task GetDetailsAsync_WhenAProviderIdIsMissing_SkipsItsEnrichmentAndRating()
	{
		var matrix = await host.Movie(Spec("The Matrix", Plex("603"))).ServedByPlex().SeedAsync();

		var results = await Testee.GetDetailsAsync([matrix]).ToListAsync();

		await AssertDetail(results[0], "The Matrix", noRating: true);
	}

	[Test]
	public async Task GetDetailsAsync_WhenTheVoteAverageIsZero_AppliesNoRating()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club", summary: "An insomniac office worker")
			.ServedByTmdb(title: "TMDB Fight Club", summary: "TMDB plot synopsis", genres: ["Drama", "Thriller"], rating: 0, votes: 0)
			.SeedAsync();

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "An insomniac office worker", genres: ["Drama", "Thriller"], noRating: true);
	}

	[Test]
	public async Task GetDetailsAsync_WhenTheEnricherReturnsNothing_StillServesAndCachesThePlexBase()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).ServedByPlex(title: "Fight Club", summary: "An insomniac office worker").SeedAsync();
		host.TmdbHasNoDetail(TestMedia.FightClub);

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "An insomniac office worker", noRating: true);
		await AssertDetailCachedAs(Detail(results[0]), fightClub, At(6, 1));
	}

	[Test]
	public async Task GetDetailsAsync_WhenPlexProvidesAField_TmdbDoesNotOverrideIt()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club", summary: "Plex summary", genres: ["Action", "Crime"])
			.ServedByTmdb(title: "TMDB Fight Club", summary: "TMDB summary", genres: ["Drama", "Thriller"], rating: 8.4, votes: 23000)
			.SeedAsync();

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "Plex summary", genres: ["Action", "Crime"], rating: RatingProvider.Tmdb);
	}

	[Test]
	public async Task GetDetailsAsync_WhenPlexOmitsSummaryAndGenres_TmdbFillsThem()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club")
			.ServedByTmdb(summary: "TMDB summary", genres: ["Drama", "Thriller"], rating: 8.4, votes: 23000)
			.SeedAsync();

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "TMDB summary", genres: ["Drama", "Thriller"], rating: RatingProvider.Tmdb);
	}

	[Test]
	public async Task GetDetailsAsync_WhenFreshButIncomplete_RehydratesTheMissingFields()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "Fight Club", summary: "Plex summary")
			.ServedByTmdb(genres: ["Drama", "Thriller"], rating: 8.4, votes: 23000)
			.SeedWithMetadataAsync(title: "Cached Title", genres: [], fetched: At(5, 31));

		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertDetail(results[0], "Fight Club", summary: "Plex summary", genres: ["Drama", "Thriller"], rating: RatingProvider.Tmdb);
		await AssertDetailCachedAs(Detail(results[0]), fightClub, At(6, 1));
	}

	[Test]
	public async Task InvalidateAsync_WhenMetadataIsInvalidated_RefetchesOnTheNextRead()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(5, 31));

		var first = await Testee.GetDetailsAsync([fightClub]).ToListAsync();
		await AssertTitle(first[0], "Cached Title");

		await Testee.InvalidateAsync([fightClub]);
		var second = await Testee.GetDetailsAsync([fightClub]).ToListAsync();
		await AssertTitle(second[0], "Live Title");
	}

	[Test]
	public async Task InvalidateAsync_WhenMetadataIsInvalidated_PreservesTheIdentityAndExternalIds()
	{
		var fightClub = await host.Movie(TestMedia.FightClub).SeedWithMetadataAsync(title: "Cached Text", fetched: At(5, 31));

		await Testee.InvalidateAsync([fightClub]);

		await AssertStoredIds(Tmdb("550"), Plex("550"));
	}

	[Test]
	public async Task InvalidateAsync_WhenTheMetadataIsFresh_StillForcesARefetch()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(6, 1));

		await Testee.InvalidateAsync([fightClub]);
		var results = await Testee.GetDetailsAsync([fightClub]).ToListAsync();

		await AssertTitle(results[0], "Live Title");
	}

	[Test]
	public async Task InvalidateAsync_WithSeveralMedia_InvalidatesThemInOneCall()
	{
		var fightClub = await host.Movie(TestMedia.FightClub)
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Title")
			.SeedWithMetadataAsync(title: "Cached Title", fetched: At(5, 31));
		var matrix = await host.Movie(Spec("The Matrix", Tmdb("603"), Plex("603")))
			.ServedByPlex(title: "")
			.ServedByTmdb(title: "Live Matrix")
			.SeedWithMetadataAsync(title: "Cached Matrix", fetched: At(5, 31));

		await Testee.InvalidateAsync([fightClub, matrix]);

		await AssertTitle((await Testee.GetDetailsAsync([fightClub]).ToListAsync())[0], "Live Title");
		await AssertTitle((await Testee.GetDetailsAsync([matrix]).ToListAsync())[0], "Live Matrix");
	}

	[Before(Test)]
	public async Task Background()
	{
		await host.EnsurePlexAccount();
		await host.EnsureTmdbAccount();
	}

	private static DateTimeOffset At(int month, int day, int hour = 0, int minute = 0) => new(2024, month, day, hour, minute, 0, TimeSpan.Zero);

	private static MediaSpec Spec(string title, params ProviderId[] ids) => new(title, MediaType.Movie, ids);

	private static MediaLookup Detail(Result<IdentifiedMedia> result) =>
		result is Success<IdentifiedMedia>(var detail) ? detail.ToMatch() : throw new InvalidOperationException($"Expected a resolved detail but got {result}.");

	private static async Task AssertTitle(Result<IdentifiedMedia> result, string title) => await Assert.That(Detail(result).Title).IsEqualTo(title);

	private static async Task AssertDetail(
		Result<IdentifiedMedia> result,
		string title,
		string? summary = null,
		IReadOnlyList<string>? genres = null,
		RatingProvider? rating = null,
		bool noRating = false
	)
	{
		var detail = Detail(result);
		await Assert.That(detail.Title).IsEqualTo(title);
		if (summary is not null)
		{
			await Assert.That(detail.Summary).IsEqualTo(summary);
		}

		if (genres is not null)
		{
			await Assert.That(detail.Genres).IsEquivalentTo(genres);
		}

		if (rating is { } source)
		{
			await Assert.That(detail.Ratings.Any(entry => entry.Source == source)).IsTrue();
		}

		if (noRating)
		{
			await Assert.That(detail.Ratings).IsEmpty();
		}
	}

	private async Task AssertCachedAs(MediaId mediaId, string title, DateTimeOffset fetched)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		var metadata = await host.ScopedDbContext.MediaMetadata.AsNoTracking().FirstAsync(entry => entry.MediaId == mediaId.Value);
		await Assert.That(metadata.Title).IsEqualTo(title);
		await Assert.That(metadata.LastFetchedAt).IsEqualTo(fetched);
	}

	private async Task AssertDetailCachedAs(MediaLookup detail, MediaId mediaId, DateTimeOffset fetched)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		var metadata = await host.ScopedDbContext.MediaMetadata.AsNoTracking().Include(entry => entry.Ratings).FirstAsync(entry => entry.MediaId == mediaId.Value);
		await Assert.That(metadata.Title).IsEqualTo(detail.Title);
		await Assert.That(metadata.Summary).IsEqualTo(detail.Summary);
		await Assert.That(metadata.Genres).IsEquivalentTo(detail.Genres);
		await Assert.That(metadata.Ratings.Select(rating => rating.Source)).IsEquivalentTo(detail.Ratings.Select(rating => rating.Source));
		await Assert.That(metadata.LastFetchedAt).IsEqualTo(fetched);
	}

	private async Task AssertStoredIds(params ProviderId[] ids)
	{
		host.ScopedDbContext.ChangeTracker.Clear();
		foreach (var id in ids)
		{
			var exists = await host.ScopedDbContext.ExternalMediaIds.AnyAsync(entry => entry.Source == id.Provider && entry.Id == id.Value);
			await Assert.That(exists).IsTrue();
		}
	}
}
