using Contracts;
using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Media;
using Domain.Media;
using Domain.Media.Metadata;
using Domain.Providers;
using Domain.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaMetadataResolverTests
{
	[Test]
	public async Task ResolveAsync_FetchesAndPushesPendingMetadataToBuffer()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var plexExternal = PlexId.From("plex-401", MediaType.Movie);
		var identity = new MediaIdentity(mediaId, [plexExternal]);
		var fresh = CompleteMovie(mediaId, plexExternal);
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(fresh.ToMatch());
		await using var buffer = new MediaResolutionBuffer(deps.MediaRepository.Object, NullLogger<MediaResolutionBuffer>.Instance);

		var result = await testee.ResolveAsync(identity, buffer);

		await Assert.That(result).IsOfUnionType<Result<MediaLookup>, Movie>();
		await Assert.That(buffer.NewMetadata.Count).IsEqualTo(1);
		await Assert.That(buffer.NewMetadata.Single().Id).IsEqualTo(mediaId);
	}

	[Test]
	public async Task ResolveAsync_WhenDetailHasNewExternals_BufferCarriesMergedExternals()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var tmdbId = TmdbId.From("404", MediaType.Movie);
		var plexId = PlexId.From("plex-404", MediaType.Movie);
		var imdbId = ImdbId.From("tt0404", MediaType.Movie);
		var identity = new MediaIdentity(mediaId, [tmdbId, plexId]);
		var fresh = CompleteMovie(mediaId, plexId) with { Identity = new MediaIdentity(mediaId, [plexId, imdbId]) };
		var (testee, deps) = CreateTestee();
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(fresh.ToMatch());
		await using var buffer = new MediaResolutionBuffer(deps.MediaRepository.Object, NullLogger<MediaResolutionBuffer>.Instance);

		await testee.ResolveAsync(identity, buffer);

		var queued = buffer.NewIdentities.Single();
		await Assert.That(queued.Externals).Contains(tmdbId);
		await Assert.That(queued.Externals).Contains(plexId);
		await Assert.That(queued.Externals).Contains(imdbId);
		await Assert.That(buffer.NewMetadata.Single().Identity.Externals).IsEquivalentTo(queued.Externals);
	}

	[Test]
	public async Task ResolveAsync_WhenEnrichmentSuppliesRating_SkipsSeparateRatingFetch()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var plexExternal = PlexId.From("plex-rating", MediaType.Movie);
		var tmdbExternal = TmdbId.From("500", MediaType.Movie);
		var identity = new MediaIdentity(mediaId, [plexExternal, tmdbExternal]);
		var (testee, deps) = CreateTestee();

		var basePartial = new PartialMovie { Identity = new MediaIdentity(mediaId, [plexExternal]), Title = "Base" };
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(IdentifiedMedia.From(basePartial).ToMatch());
		var enriched = new PartialMovie
		{
			Identity = new MediaIdentity(mediaId, [tmdbExternal]),
			Title = "Enriched",
			Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 8f, 500)]),
		};
		deps.Enricher.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(IdentifiedMedia.From(enriched).ToMatch());
		await using var buffer = new MediaResolutionBuffer(deps.MediaRepository.Object, NullLogger<MediaResolutionBuffer>.Instance);

		await testee.ResolveAsync(identity, buffer);

		deps.Rating.GetRatingsAsync(Any<IExternalMediaId>(), Any()).WasCalled(Times.Never);
	}

	private static (MediaMetadataResolver Testee, Dependencies Deps) CreateTestee()
	{
		var mediaRepository = IMediaRepository.Mock();
		mediaRepository.UpsertIdentitiesAsync(Any<IReadOnlyList<MediaIdentity>>(), Any()).Returns((identities, _) => [.. identities]);
		mediaRepository.FindIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any()).Returns(new MediaIdentity?[] { null });
		var search = IMediaSearchProvider.Mock();
		var enricher = IMetadataEnricher.Mock();
		var rating = IMediaRatingService.Mock();

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

		var searchIndex = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);
		var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

		var testee = new MediaMetadataResolver(searchIndex, enricherIndex, ratingIndex, configRepository.Object);
		return (testee, new Dependencies(mediaRepository, search, enricher, rating));
	}

	private static Movie CompleteMovie(MediaId mediaId, IExternalMediaId external) =>
		new()
		{
			Identity = new MediaIdentity(mediaId, [external]),
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
		Mock<IMediaRepository> MediaRepository,
		Mock<IMediaSearchProvider> Search,
		Mock<IMetadataEnricher> Enricher,
		Mock<IMediaRatingService> Rating
	);
}
