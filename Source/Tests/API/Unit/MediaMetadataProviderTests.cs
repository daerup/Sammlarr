using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Test.Shared;

namespace API.Tests.Unit;

public class MediaMetadataProviderTests
{
	private static readonly DateTimeOffset _now = new(2026, 5, 6, 12, 0, 0, TimeSpan.Zero);

	[Test]
	public async Task GetAsync_WhenCachedAndComplete_ReturnsCachedWithoutFetching()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = TmdbId.From("400", MediaType.Movie);
		var cached = CompleteMovie(mediaId, external);
		var (testee, deps) = CreateTestee(mediaId, [external], cached: cached, cachedAt: _now.AddDays(-1));

		var detail = await testee.GetAsync(mediaId);

		await Assert.That(detail).IsSameReferenceAs(cached);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasNeverCalled();
		deps.Repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task GetAsync_WhenNotCached_FetchesAndUpserts()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var external = TmdbId.From("401", MediaType.Movie);
		var plexExternal = PlexId.From("plex-401", MediaType.Movie);
		var fresh = CompleteMovie(mediaId, plexExternal);
		var (testee, deps) = CreateTestee(mediaId, [external, plexExternal], cached: null);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(fresh);

		var detail = await testee.GetAsync(mediaId);

		await Assert.That(detail).IsEqualTo(fresh);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Once);
		deps.Repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenDetailHasNewExternals_UpsertsThemIntoMediaRow()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var tmdbId = TmdbId.From("404", MediaType.Movie);
		var plexId = PlexId.From("plex-404", MediaType.Movie);
		var imdbId = ImdbId.From("tt0404", MediaType.Movie);
		var fresh = CompleteMovie(mediaId, plexId) with { Externals = [plexId, imdbId] };
		var (testee, deps) = CreateTestee(mediaId, [tmdbId, plexId], cached: null);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(fresh);

		await testee.GetAsync(mediaId);

		deps.MediaRepo.UpsertAsync(
				Arg.Is<IEnumerable<IExternalMediaId>>(ids =>
					ids.Contains((IExternalMediaId)tmdbId) && ids.Contains((IExternalMediaId)plexId) && ids.Contains((IExternalMediaId)imdbId)
				),
				Any()
			)
			.WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenDetailExternalsAreSubsetOfIdentity_DoesNotUpsertMediaRow()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var tmdbId = TmdbId.From("405", MediaType.Movie);
		var plexId = PlexId.From("plex-405", MediaType.Movie);
		var fresh = CompleteMovie(mediaId, plexId);
		var (testee, deps) = CreateTestee(mediaId, [tmdbId, plexId], cached: null);
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(fresh);

		await testee.GetAsync(mediaId);

		deps.MediaRepo.UpsertAsync(Any<IEnumerable<IExternalMediaId>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task GetAsync_WhenMediaIdHasNoIdentity_ReturnsNull()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee(mediaId, externals: null, cached: null);

		var detail = await testee.GetAsync(mediaId);

		await Assert.That(detail).IsNull();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).WasNeverCalled();
		deps.Repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasNeverCalled();
	}

	[Test]
	public async Task InvalidateAsync_DelegatesToRepositoryRemoveManyOnce()
	{
		var first = MediaId.From(Guid.NewGuid());
		var second = MediaId.From(Guid.NewGuid());
		var (testee, deps) = CreateTestee(first, [], cached: null);

		await testee.InvalidateAsync([first, second]);

		deps.Repository.RemoveManyAsync(Any<IReadOnlyList<MediaId>>(), Any()).WasCalled(Times.Once);
	}

	private static (MediaMetadataProvider Testee, Dependencies Deps) CreateTestee(
		MediaId mediaId,
		IReadOnlyList<IExternalMediaId>? externals,
		IMediaDetailResult? cached,
		DateTimeOffset cachedAt = default
	)
	{
		var repository = IMediaMetadataRepository.Mock();
		repository.GetByMediaIdAsync(Any(), Any()).Returns(cached is null ? null : new CachedMediaMetadata(cached, cachedAt == default ? _now : cachedAt));

		var mediaRepo = IMediaRepository.Mock();
		mediaRepo.FindAsync(Arg.Is<MediaId>(id => id == mediaId), Any()).Returns(externals is null ? null : new MediaIdentity(mediaId, externals.ToList()));

		var search = IMediaSearchService.Mock();
		var enricher = IMetadataEnricher.Mock();
		var rating = IMediaRatingService.Mock();
		var dateTimeProvider = IDateTimeProvider.Mock();
		dateTimeProvider.Now.Returns(_now);

		var configRepository = IProviderConfigRepository.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = MediaSearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
				}
			);

		var searchIndex = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchService>().Register(MediaSearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);
		var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

		var testee = new MediaMetadataProvider(
			mediaRepo.Object,
			repository.Object,
			searchIndex,
			enricherIndex,
			ratingIndex,
			configRepository.Object,
			dateTimeProvider.Object
		);
		return (testee, new Dependencies(repository, search, enricher, rating, mediaRepo));
	}

	private static MovieDetailResult CompleteMovie(MediaId mediaId, IExternalMediaId external) =>
		new()
		{
			Id = mediaId,
			Externals = new List<IExternalMediaId> { external },
			Title = "Complete",
			Tagline = "Tagline",
			Summary = "Summary",
			PosterUrl = "https://example.com/poster.jpg",
			BackdropUrl = "https://example.com/backdrop.jpg",
			ReleaseDate = new DateOnly(2024, 1, 1),
			Genres = ["Action"],
			Runtime = TimeSpan.FromMinutes(120),
		};

	private record Dependencies(
		Mock<IMediaMetadataRepository> Repository,
		Mock<IMediaSearchService> Search,
		Mock<IMetadataEnricher> Enricher,
		Mock<IMediaRatingService> Rating,
		Mock<IMediaRepository> MediaRepo
	);
}
