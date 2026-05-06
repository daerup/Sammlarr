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
	public async Task GetAsync_WhenCachedAndCompleteWithin7Days_ReturnsCachedWithoutRefetch()
	{
		var mediaId = TmdbId.From("100", MediaType.Movie);
		var cached = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cachedAt: _now.AddDays(-6), cached: cached);

		var result = await testee.GetAsync(mediaId);

		await Assert.That(result).IsSameReferenceAs(cached);
		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Never);
		deps.Repository.UpsertAsync(mediaId, Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task GetAsync_WhenCachedAndIncompleteWithinHydrationCooldown_ReturnsCachedWithoutHydrating()
	{
		var mediaId = TmdbId.From("110", MediaType.Movie);
		var cached = IncompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cachedAt: _now.AddMinutes(-30), cached: cached);

		var result = await testee.GetAsync(mediaId);

		await Assert.That(result).IsSameReferenceAs(cached);
		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Never);
		deps.Enricher.LookUpAsync(Any(), Any()).WasCalled(Times.Never);
		deps.Rating.GetRatingsAsync(Any(), Any()).WasCalled(Times.Never);
		deps.Repository.UpsertAsync(mediaId, Any(), Any()).WasCalled(Times.Never);
	}

	[Test]
	public async Task GetAsync_WhenCachedAndIncompletePastHydrationCooldown_HydratesWithoutRefetchingSearch()
	{
		var mediaId = TmdbId.From("120", MediaType.Movie);
		var cached = IncompleteMovie(mediaId);
		var enrichment = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cachedAt: _now.AddHours(-2), cached: cached);
		deps.Enricher.LookUpAsync(Any(), Any()).Returns(enrichment);

		var result = await testee.GetAsync(mediaId);

		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Never);
		deps.Enricher.LookUpAsync(Any(), Any()).WasCalled(Times.AtLeastOnce);
		deps.Repository.UpsertAsync(mediaId, Any(), Any()).WasCalled(Times.Once);
		await Assert.That(result.Tagline).IsEqualTo(enrichment.Tagline);
	}

	[Test]
	public async Task GetAsync_WhenCachedAndIncompleteOlderThanStaleness_RefetchesViaSearch()
	{
		var mediaId = TmdbId.From("140", MediaType.Movie);
		var cached = IncompleteMovie(mediaId);
		var fresh = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cachedAt: _now.AddDays(-8), cached: cached);
		deps.Search.LookUpAsync(mediaId, Any()).Returns(fresh);

		var result = await testee.GetAsync(mediaId);

		await Assert.That(result).IsSameReferenceAs(fresh);
		deps.Repository.UpsertAsync(mediaId, fresh, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenCachedAndCompleteOlderThan7Days_Refetches()
	{
		var mediaId = TmdbId.From("130", MediaType.Movie);
		var cached = CompleteMovie(mediaId);
		var fresh = CompleteMovie(mediaId) with { Title = "Refreshed" };
		var (testee, deps) = CreateTestee(cachedAt: _now.AddDays(-8), cached: cached);
		deps.Search.LookUpAsync(mediaId, Any()).Returns(fresh);

		var result = await testee.GetAsync(mediaId);

		await Assert.That(result).IsSameReferenceAs(fresh);
		deps.Repository.UpsertAsync(mediaId, fresh, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task GetAsync_WhenCacheMiss_RunsFullFetchPipelineAndUpserts()
	{
		var mediaId = PlexId.From("plex-1", MediaType.Movie);
		var fresh = CompleteMovie(mediaId);
		var (testee, deps) = CreateTestee(cached: null);
		deps.Search.LookUpAsync(mediaId, Any()).Returns(fresh);

		var result = await testee.GetAsync(mediaId);

		await Assert.That(result).IsSameReferenceAs(fresh);
		deps.Search.LookUpAsync(mediaId, Any()).WasCalled(Times.Once);
		deps.Repository.UpsertAsync(mediaId, fresh, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task ReportStaleAsync_DelegatesToRepositoryRemove()
	{
		var mediaId = TmdbId.From("200", MediaType.Movie);
		var (testee, deps) = CreateTestee(cached: null);

		await testee.ReportStaleAsync(mediaId);

		deps.Repository.RemoveAsync(mediaId, Any()).WasCalled(Times.Once);
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
