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
		var mediaId = TmdbId.From("400", MediaType.Movie);
		var cached = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cached: cached, cachedAt: _now.AddDays(-1));

		var detail = await testee.GetAsync(mediaId);

		await Assert.That(detail).IsSameReferenceAs(cached);
		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Never);
		deps.Repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task GetAsync_WhenNotCached_FetchesAndUpserts()
	{
		var mediaId = TmdbId.From("401", MediaType.Movie);
		var fresh = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cached: null);
		deps.Search.LookUpAsync(mediaId, Any()).Returns(fresh);

		var detail = await testee.GetAsync(mediaId);

		await Assert.That(detail).IsSameReferenceAs(fresh);
		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Once);
		deps.Repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task InvalidateAsync_DelegatesToRepositoryRemoveManyOnce()
	{
		var first = TmdbId.From("200", MediaType.Movie);
		var second = TmdbId.From("201", MediaType.Movie);
		var (testee, deps) = CreateTestee(cached: null);

		await testee.InvalidateAsync([first, second]);

		deps.Repository.RemoveManyAsync(Any<IReadOnlyList<IMediaId>>(), Any()).WasCalled(Times.Once);
	}

	private static (MediaMetadataProvider Testee, Dependencies Deps) CreateTestee(IMediaDetailResult? cached, DateTimeOffset cachedAt = default)
	{
		var repository = IMediaMetadataRepository.Mock();
		repository.GetByMediaIdAsync(Any(), Any()).Returns(cached is null ? null : new CachedMediaMetadata(cached, cachedAt == default ? _now : cachedAt));

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

		var testee = new MediaMetadataProvider(repository.Object, searchIndex, enricherIndex, ratingIndex, configRepository.Object, dateTimeProvider.Object);
		return (testee, new Dependencies(repository, search, enricher, rating));
	}

	private static MovieDetailResult CompleteMovie(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = [],
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
		Mock<IMediaRatingService> Rating
	);
}
