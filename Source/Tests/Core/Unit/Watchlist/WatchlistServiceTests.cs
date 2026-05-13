using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Contracts.Watchlist;
using Core.Watchlist;
using Test.Shared;

namespace Core.Tests.Unit.Watchlist;

public class WatchlistServiceTests
{
	[Test]
	public async Task GetWatchlistAsync_WhenProviderReturnsItems_EnrichesEachItemWithMetadata()
	{
		var firstId = PlexId.From("plex-1", MediaType.Movie);
		var secondId = PlexId.From("plex-2", MediaType.Show);
		var firstDetail = CreateMovieDetail(firstId);
		var secondDetail = CreateShowDetail(secondId);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(1, Any()).Returns(new WatchlistProviderPage([firstId, secondId], 7));

		var metadataProvider = IMediaMetadataProvider.Mock();
		metadataProvider.GetAsync(firstId, Any()).Returns(firstDetail);
		metadataProvider.GetAsync(secondId, Any()).Returns(secondDetail);

		var testee = CreateTestee(provider, metadataProvider);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.TotalPages).IsEqualTo(7);
		await Assert.That(result.Items).Count().IsEqualTo(2);
		await Assert.That(result.Items).Contains(firstDetail);
		await Assert.That(result.Items).Contains(secondDetail);
	}

	[Test]
	public async Task GetWatchlistAsync_WhenProviderReturnsEmptyPage_ReturnsEmptyResult()
	{
		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(1, Any()).Returns(new WatchlistProviderPage([], 0));

		var testee = CreateTestee(provider);

		var result = await testee.GetWatchlistAsync(1);

		await Assert.That(result.Items).IsEmpty();
		await Assert.That(result.TotalPages).IsEqualTo(0);
	}

	[Test]
	public async Task GetWatchlistPagedAsync_WhenMultiplePages_StreamsAllItemsAcrossPages()
	{
		var page1Id = PlexId.From("plex-1", MediaType.Movie);
		var page2Id = PlexId.From("plex-2", MediaType.Movie);
		var detail1 = CreateMovieDetail(page1Id);
		var detail2 = CreateMovieDetail(page2Id);

		var provider = IWatchlistProvider.Mock();
		provider.GetWatchlistAsync(1, Any()).Returns(new WatchlistProviderPage([page1Id], 2));
		provider.GetWatchlistAsync(2, Any()).Returns(new WatchlistProviderPage([page2Id], 2));

		var metadataProvider = IMediaMetadataProvider.Mock();
		metadataProvider.GetAsync(page1Id, Any()).Returns(detail1);
		metadataProvider.GetAsync(page2Id, Any()).Returns(detail2);

		var testee = CreateTestee(provider, metadataProvider);

		var collected = new List<IMediaDetailResult>();
		await foreach (var item in testee.GetWatchlistAsync())
		{
			collected.Add(item);
		}

		await Assert.That(collected).Count().IsEqualTo(2);
		await Assert.That(collected[0]).IsSameReferenceAs(detail1);
		await Assert.That(collected[1]).IsSameReferenceAs(detail2);
	}

	[Test]
	public async Task AddToWatchlistAsync_DelegatesToResolvedProvider()
	{
		var mediaId = PlexId.From("plex-add", MediaType.Movie);
		var provider = IWatchlistProvider.Mock();
		provider.AddToWatchlistAsync(mediaId, Any()).Returns(true);

		var testee = CreateTestee(provider);

		var result = await testee.AddToWatchlistAsync(mediaId);

		await Assert.That(result).IsTrue();
		provider.AddToWatchlistAsync(mediaId, Any()).WasCalled(Times.Once);
	}

	[Test]
	public async Task RemoveFromWatchlistAsync_DelegatesToResolvedProvider()
	{
		var mediaId = PlexId.From("plex-remove", MediaType.Movie);
		var provider = IWatchlistProvider.Mock();
		provider.RemoveFromWatchlistAsync(mediaId, Any()).Returns(false);

		var testee = CreateTestee(provider);

		var result = await testee.RemoveFromWatchlistAsync(mediaId);

		await Assert.That(result).IsFalse();
		provider.RemoveFromWatchlistAsync(mediaId, Any()).WasCalled(Times.Once);
	}

	private static WatchlistService CreateTestee(Mock<IWatchlistProvider> provider, Mock<IMediaMetadataProvider>? metadataProvider = null)
	{
		var index = new FakeKeyedServiceIndex<WatchlistProvider, IWatchlistProvider>().Register(WatchlistProvider.Plex, provider.Object);
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
		var resolvedMetadataProvider = metadataProvider ?? IMediaMetadataProvider.Mock();
		return new WatchlistService(index, configRepository.Object, resolvedMetadataProvider.Object);
	}

	private static MovieDetailResult CreateMovieDetail(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = [],
			Title = "Movie",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Runtime = TimeSpan.Zero,
		};

	private static ShowDetailResult CreateShowDetail(IMediaId sourceId) =>
		new()
		{
			SourceId = sourceId,
			AdditionalIds = [],
			Title = "Show",
			Tagline = string.Empty,
			Summary = string.Empty,
			PosterUrl = string.Empty,
			BackdropUrl = string.Empty,
			ReleaseDate = DateOnly.MinValue,
			Genres = [],
			Seasons = 1,
			Episodes = 10,
			RuntimePerEpisode = TimeSpan.Zero,
		};
}
