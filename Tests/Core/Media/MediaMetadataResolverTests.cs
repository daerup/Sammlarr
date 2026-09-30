using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
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
	public async Task ResolveAsync_WhenEnrichmentSuppliesRating_SkipsSeparateRatingFetch()
	{
		var mediaId = MediaId.From(Guid.NewGuid());
		var plexExternal = PlexId.From("plex-rating", MediaType.Movie);
		var tmdbExternal = TmdbId.From("500", MediaType.Movie);
		var identity = new MediaIdentity(mediaId, [plexExternal, tmdbExternal]);
		var (testee, deps) = CreateTestee();

		var basePartial = new PartialMovie { Identity = new MediaIdentity(mediaId, [plexExternal]), Title = "Base" };
		deps.Search.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(IdentifiedMedia.From(basePartial).ToMatch()));
		var enriched = new PartialMovie
		{
			Identity = new MediaIdentity(mediaId, [tmdbExternal]),
			Title = "Enriched",
			Rating = RatingSummary.From([new MediaRating(RatingProvider.Tmdb, 8f, 500)]),
		};
		deps.Enricher.LookUpAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of(IdentifiedMedia.From(enriched).ToMatch()));
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
		rating.GetRatingsAsync(Any<IExternalMediaId>(), Any()).Returns(Success.Of<IReadOnlyList<MediaRating>>([]));

		var configRepository = IProviderConfigService.Mock();
		configRepository
			.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					Sink = SinkKind.Plex,
					TrendingProvider = TrendingProvider.Tmdb,
					SearchProvider = SearchProvider.Plex,
					RatingProvider = RatingProvider.Tmdb,
					AuthProvider = AuthProvider.Plex,
					RegistrationMode = RegistrationMode.Open,
				}
			);

		var searchIndex = new FakeKeyedServiceIndex<SearchProvider, IMediaSearchProvider>().Register(SearchProvider.Plex, search.Object);
		var enricherIndex = new FakeKeyedServiceIndex<MediaProvider, IMetadataEnricher>()
			.Register(MediaProvider.Plex, enricher.Object)
			.Register(MediaProvider.Tmdb, enricher.Object);
		var ratingIndex = new FakeKeyedServiceIndex<RatingProvider, IMediaRatingService>().Register(RatingProvider.Tmdb, rating.Object);

		var testee = new MediaMetadataResolver(searchIndex, enricherIndex, ratingIndex, configRepository.Object);
		return (testee, new Dependencies(mediaRepository, search, enricher, rating));
	}

	private record Dependencies(
		Mock<IMediaRepository> MediaRepository,
		Mock<IMediaSearchProvider> Search,
		Mock<IMetadataEnricher> Enricher,
		Mock<IMediaRatingService> Rating
	);
}
