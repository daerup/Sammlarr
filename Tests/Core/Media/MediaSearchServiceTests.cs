using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Core.Media;
using Domain.Media;
using Domain.Providers;
using Domain.Settings;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaSearchServiceTests
{
	[Test]
	public async Task SearchAsync_DispatchesToConfiguredProvider_AndStampsReconciledIdentities()
	{
		var plexId = PlexId.From("plex-1", MediaType.Movie);
		var raw = BuildRawMovie([plexId]);
		var provider = IMediaSearchProvider.Mock();
		provider.SearchAsync("matrix", Any<IReadOnlyCollection<MediaType>>(), Any()).Returns([raw]);
		var providers = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, provider.Object);
		var resolvedId = MediaId.From(Guid.NewGuid());
		var mediaService = IMediaService.Mock();
		mediaService.ResolveIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any()).Returns([new MediaIdentity(resolvedId, [plexId])]);
		var configRepository = ConfigWithSearchProvider(MediaSearchProvider.Plex);
		var testee = new MediaSearchService(providers, mediaService.Object, configRepository.Object);

		var results = await testee.SearchAsync("matrix", [MediaType.Movie]);

		await Assert.That(results.Count).IsEqualTo(1);
		await Assert.That(results[0].Identity.Id).IsEqualTo(resolvedId);
		await Assert.That(results[0].Identity.Externals).Contains((IExternalMediaId)plexId);
	}

	[Test]
	public async Task SearchAsync_WhenProviderReturnsEmpty_DoesNotCallResolveIdentities()
	{
		var provider = IMediaSearchProvider.Mock();
		provider.SearchAsync(Any(), Any<IReadOnlyCollection<MediaType>>(), Any()).Returns([]);
		var providers = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, provider.Object);
		var resolveCalls = 0;
		var mediaService = IMediaService.Mock();
		mediaService
			.ResolveIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any())
			.Returns(
				(_, _) =>
				{
					resolveCalls++;
					return [];
				}
			);
		var configRepository = ConfigWithSearchProvider(MediaSearchProvider.Plex);
		var testee = new MediaSearchService(providers, mediaService.Object, configRepository.Object);

		var results = await testee.SearchAsync("nothing", [MediaType.Movie]);

		await Assert.That(results).IsEmpty();
		await Assert.That(resolveCalls).IsEqualTo(0);
	}

	[Test]
	public async Task SearchAsync_PassesProviderExternalsAsCandidateGroups()
	{
		var plexId = PlexId.From("plex-77", MediaType.Movie);
		var tmdbId = TmdbId.From("777", MediaType.Movie);
		var raw = BuildRawMovie([plexId, tmdbId]);
		var provider = IMediaSearchProvider.Mock();
		provider.SearchAsync(Any(), Any<IReadOnlyCollection<MediaType>>(), Any()).Returns([raw]);
		var providers = new FakeKeyedServiceIndex<MediaSearchProvider, IMediaSearchProvider>().Register(MediaSearchProvider.Plex, provider.Object);
		IReadOnlyList<IReadOnlyList<IExternalMediaId>>? captured = null;
		var mediaService = IMediaService.Mock();
		mediaService
			.ResolveIdentitiesAsync(Any<IReadOnlyList<IReadOnlyList<IExternalMediaId>>>(), Any())
			.Returns(
				(groups, _) =>
				{
					captured = groups;
					return [new MediaIdentity(MediaId.From(Guid.NewGuid()), [.. groups[0]])];
				}
			);
		var configRepository = ConfigWithSearchProvider(MediaSearchProvider.Plex);
		var testee = new MediaSearchService(providers, mediaService.Object, configRepository.Object);

		await testee.SearchAsync("anything", [MediaType.Movie]);

		await Assert.That(captured).IsNotNull();
		await Assert.That(captured!.Count).IsEqualTo(1);
		await Assert.That(captured[0]).Contains(plexId);
		await Assert.That(captured[0]).Contains(tmdbId);
	}

	private static Mock<IProviderConfigRepository> ConfigWithSearchProvider(MediaSearchProvider provider)
	{
		var repo = IProviderConfigRepository.Mock();
		repo.GetAsync(Any())
			.Returns(
				new ProviderConfig
				{
					WatchlistProvider = WatchlistProvider.Plex,
					TrendingProvider = MediaRecommendationProvider.Tmdb,
					SearchProvider = provider,
					RatingProvider = RatingProvider.Tmdb,
				}
			);
		return repo;
	}

	private static MediaLookup BuildRawMovie(IReadOnlyList<IExternalMediaId> externals) =>
		new PartialMovie { Identity = new MediaIdentity(default, [.. externals]), Title = "Title" }.ToMatch();
}
