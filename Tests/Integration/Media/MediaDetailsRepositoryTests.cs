using DB.Data.Media;
using Domain.Media;
using Harness;
using Microsoft.EntityFrameworkCore;
using TestInfrastructure;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaDetailsRepositoryTests(SammlarrTestHost fixture) : IAsyncDisposable
{
	private static readonly MediaFields Gaps =
		MediaField.Tagline | MediaField.Backdrop | MediaFields.Rated([RatingSource.Imdb, RatingSource.RottenTomatoesAudience]);

	private static readonly DateTimeOffset FetchedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

	private MediaRepository _testee = null!;

	public async ValueTask DisposeAsync() => await fixture.DisposeAsync();

	[Before(Test)]
	public void Setup()
	{
		_testee = new MediaRepository(fixture.ContextFactory, new MediaDetailsFactory(), new MediaWriteLock());
	}

	private async Task<MediaId> SeedMediaAsync(IEnumerable<IExternalMediaId> ids)
	{
		var media = new MediaRow();
		await fixture.Context.ExternalMediaIds.AddRangeAsync(ids.Select(id => id.ToExternalIdEntity(media)));
		await fixture.Context.SaveChangesAsync();
		fixture.Context.ChangeTracker.Clear();
		return MediaId.From(media.Id);
	}

	[Test]
	public async Task UpsertDetailsAsync_WhenFetchedAgain_AdvancesFetchedAtEvenWhenThePayloadIsIdentical()
	{
		var plexId = PlexId.From("plex-700", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var secondFetch = FetchedAt.AddHours(2);

		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete())));
		fixture.Context.ChangeTracker.Clear();
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete()) with { FetchedAt = secondFetch }));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();
		await Assert.That(stored.Cached).IsSome().And.Multiple(async cached => await Assert.That(cached.FetchedAt).IsEqualTo(secondFetch));
	}

	[Test]
	public async Task UpsertDetailsAsync_WhenEntryExists_PreservesCreatedAt()
	{
		var plexId = PlexId.From("plex-500", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete())));
		fixture.Context.ChangeTracker.Clear();

		var marker = new DateTimeOffset(2020, 6, 15, 0, 0, 0, TimeSpan.Zero);
		await fixture.Context.MediaDetails.ExecuteUpdateAsync(s => s.SetProperty(m => m.CreatedAt, marker));

		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete() with { Title = "Updated" })));
		fixture.Context.ChangeTracker.Clear();

		var stored = await fixture.Context.MediaDetails.AsNoTracking().SingleAsync();
		using var _ = Assert.Multiple();
		await Assert.That(stored.Title).IsEqualTo("Updated");
		await Assert.That(stored.CreatedAt).IsEqualTo(marker);
	}

	[Test]
	public async Task GetAsync_WhenDetailsExist_YieldsThemWithTheirIdentityAndFetchedAt()
	{
		var plexId = PlexId.From("plex-810", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete())));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();

		await Assert.That(stored.Identity).IsEqualTo(new MediaIdentity(mediaId, [plexId]));
		await Assert.That(stored.Cached).IsSome().And.Multiple(async cached => await Assert.That(cached).IsEqualTo(Cached(Complete())));
	}

	[Test]
	public async Task UpsertDetailsAsync_ForAPartial_KeepsWhichDetailsAndRatingSourcesAreConfirmedAbsent()
	{
		var plexId = PlexId.From("plex-812", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);

		await _testee.UpsertDetailsAsync(Details(mediaId, new CachedDetails(new MovieDetails { Title = "Solaris" }, FetchedAt, Gaps)));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();
		await Assert.That(stored.Cached).IsSome().And.Multiple(async cached => await Assert.That(cached.ConfirmedAbsent).IsEqualTo(Gaps));
	}

	[Test]
	public async Task GetAsync_WhenNoDetailsExist_YieldsTheIdentityWithoutCachedDetails()
	{
		var plexId = PlexId.From("plex-811", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);

		var stored = (await _testee.GetAsync([mediaId])).Single();

		await Assert.That(stored.Identity.Id).IsEqualTo(mediaId);
		await Assert.That(stored.Cached).IsNone();
	}

	[Test]
	public async Task GetAsync_WhenOneTitleHasDetailsAndOneHasNone_YieldsBothInOneRoundTrip()
	{
		var firstPlex = PlexId.From("plex-812", MediaType.Movie);
		var secondPlex = PlexId.From("plex-813", MediaType.Movie);
		var firstId = await SeedMediaAsync([firstPlex]);
		var secondId = await SeedMediaAsync([secondPlex]);
		await _testee.UpsertDetailsAsync(Details(firstId, Cached(Complete())));
		fixture.Context.ChangeTracker.Clear();

		var stored = await _testee.GetAsync([firstId, secondId]);

		await Assert.That(stored.Count).IsEqualTo(2);
		await Assert.That(stored.Single(item => item.Identity.Id == firstId).Cached).IsSome();
		await Assert.That(stored.Single(item => item.Identity.Id == secondId).Cached).IsNone();
	}

	[Test]
	public async Task GetAsync_WhenRatingsExist_RoundTripsRatingsThroughInclude()
	{
		var plexId = PlexId.From("plex-814", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);
		var rated = Complete() with { Ratings = MediaRatings.From([new TmdbRating(8.5f, Some.Of(1200))]) };
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(rated)));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();

		await Assert
			.That(stored.Cached)
			.IsSome()
			.And.Multiple(async cached =>
			{
				var rating = cached.Details.Ratings.All.Single();
				await Assert.That(rating.Source).IsEqualTo(RatingSource.Tmdb);
				await Assert.That(rating.SammlarrValue).IsEqualTo(8.5f);
				await Assert.That(rating.Votes).IsSome().And.IsEqualTo(1200);
			});
	}

	[Test]
	public async Task GetAsync_WhenEverySourceRatedTheTitle_RoundTripsEachRatingType()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-816", MediaType.Movie)]);
		var ratings = MediaRatings.From([
			new TmdbRating(8.6f, Some.Of(7705)),
			new ImdbRating(8.2f, Some.Of(542195)),
			new RottenTomatoesCriticsRating(64),
			new RottenTomatoesAudienceRating(95),
		]);
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete() with { Ratings = ratings })));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();

		await Assert.That(stored.Cached).IsSome().And.Multiple(async cached => await Assert.That(cached.Details.Ratings).IsEqualTo(ratings));
	}

	[Test]
	public async Task GetAsync_WhenAMediaHasSeveralIdsAndRatings_ReadsEachOnce()
	{
		var mediaId = await SeedMediaAsync([PlexId.From("plex-815", MediaType.Movie), TmdbId.From("815", MediaType.Movie), ImdbId.From("tt0815", MediaType.Movie)]);
		var rated = Complete() with { Ratings = MediaRatings.From([new TmdbRating(8.5f, Some.Of(1200)), new ImdbRating(7.0f, new None())]) };
		await _testee.UpsertDetailsAsync(Details(mediaId, Cached(rated)));
		fixture.Context.ChangeTracker.Clear();

		var stored = (await _testee.GetAsync([mediaId])).Single();

		await Assert.That(stored.Identity.Externals).Count().IsEqualTo(3);
		await Assert.That(stored.Cached).IsSome().And.Multiple(async cached => await Assert.That(cached.Details.Ratings.All).Count().IsEqualTo(2));
	}

	[Test]
	public async Task UpsertDetailsAsync_WhenConcurrentFlushesShareAMediaId_DoesNotViolateTheUniqueConstraint()
	{
		var plexId = PlexId.From("plex-race", MediaType.Movie);
		var mediaId = await SeedMediaAsync([plexId]);

		var flushes = Enumerable.Range(0, 8).Select(_ => Task.Run(() => _testee.UpsertDetailsAsync(Details(mediaId, Cached(Complete())))));

		await Assert.That(async () => await Task.WhenAll(flushes)).ThrowsNothing();
	}

	private static Dictionary<MediaId, CachedDetails> Details(MediaId id, CachedDetails cached) => new() { [id] = cached };

	private static CachedDetails Cached(MediaDetails details) => new(details, FetchedAt, MediaField.None);

	private static MovieDetails Complete()
	{
		return new()
		{
			Title = "Title",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Ratings = MediaRatings.From([new TmdbRating(7.5f, Some.Of(100))]),
			Runtime = TimeSpan.FromMinutes(120),
		};
	}
}
