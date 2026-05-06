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
	public async Task GetAsync_Batch_StreamsResultsAndDefersDbWritesUntilAfterParallelFetch()
	{
		var cachedId = TmdbId.From("400", MediaType.Movie);
		var missingId = TmdbId.From("401", MediaType.Movie);
		var cachedDetails = CompleteMovie(cachedId);
		var freshDetails = CompleteMovie(missingId);

		var repository = IMediaMetadataRepository.Mock();
		repository.GetByMediaIdAsync(cachedId, Any()).Returns(new CachedMediaMetadata(cachedDetails, _now.AddDays(-1)));
		repository.GetByMediaIdAsync(missingId, Any()).Returns((CachedMediaMetadata?)null);

		var search = IMediaSearchService.Mock();
		search.LookUpAsync(missingId, Any()).Returns(freshDetails);

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

		var collected = new List<IMediaDetailResult>();
		await foreach (var detail in testee.GetAsync(new IMediaId[] { cachedId, missingId }))
		{
			collected.Add(detail);
		}

		await Assert.That(collected).Count().IsEqualTo(2);
		await Assert.That(collected).Contains(cachedDetails);
		await Assert.That(collected).Contains(freshDetails);
		search.LookUpAsync(cachedId, Any()).WasCalled(Times.Never);
		search.LookUpAsync(missingId, Any()).WasCalled(Times.Once);
		repository.UpsertManyAsync(Any<IReadOnlyList<MediaMetadataUpsert>>(), Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_Batch_WhenEmpty_YieldsNothing()
	{
		var (testee, _) = CreateTestee(cached: null);

		var collected = new List<IMediaDetailResult>();
		await foreach (var detail in testee.GetAsync(Array.Empty<IMediaId>()))
		{
			collected.Add(detail);
		}

		await Assert.That(collected).IsEmpty();
	}

	[Test]
	public async Task InvalidateAsync_DelegatesToRepositoryRemoveForEachId()
	{
		var first = TmdbId.From("200", MediaType.Movie);
		var second = TmdbId.From("201", MediaType.Movie);
		var (testee, deps) = CreateTestee(cached: null);

		await testee.InvalidateAsync([first, second]);

		deps.Repository.RemoveAsync(first, Any()).WasCalled(Times.Once);
		deps.Repository.RemoveAsync(second, Any()).WasCalled(Times.Once);
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

	private static MovieDetailResult IncompleteMovie(IMediaId sourceId) => CompleteMovie(sourceId) with { Tagline = string.Empty };

	private record Dependencies(
		Mock<IMediaMetadataRepository> Repository,
		Mock<IMediaSearchService> Search,
		Mock<IMetadataEnricher> Enricher,
		Mock<IMediaRatingService> Rating
	);
}
