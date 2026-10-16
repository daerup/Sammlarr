using Contracts.Media;
using Contracts.Media.Search;
using Core.DI;
using Core.Media;
using Core.Tests.Settings;
using Domain.Media;
using Domain.Providers;
using Microsoft.Extensions.DependencyInjection;
using TestInfrastructure;

namespace Core.Tests.Media;

public class MediaSearchServiceTests
{
	private static readonly IIdentityKey Plex = PlexId.From("annie-1982", MediaType.Movie);
	private static readonly IIdentityKey Tmdb = TmdbId.From("15739", MediaType.Movie);
	private static readonly MediaIdentity Owner = new(MediaId.New(), [Plex]);

	[Test]
	public async Task SearchAsync_ForAHitSpanningTwoTitles_UsesTheOneHoldingTheSearchsOwnId()
	{
		var media = IMediaService.Mock();
		media.RegisterAsync(Any(), Any(), Any()).Returns([new None()]);
		var repository = IMediaRepository.Mock();
		repository.GetByExternalsAsync(Any(), Any()).Returns([new StoredMedia(Owner, new None())]);

		var results = await Testee(media, repository).SearchAsync("Annie", [MediaType.Movie]);

		var matched = await Assert.That(results.Results.Single()).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(matched!.Identity).IsEqualTo(Owner);
	}

	[Test]
	public async Task SearchAsync_ForAHitSpanningTwoTitlesWhenNoTitleHoldsTheSearchsOwnId_IsNotAddable()
	{
		var media = IMediaService.Mock();
		media.RegisterAsync(Any(), Any(), Any()).Returns([new None()]);
		var repository = IMediaRepository.Mock();
		repository.GetByExternalsAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());

		var results = await Testee(media, repository).SearchAsync("Annie", [MediaType.Movie]);

		var unmatched = await Assert.That(results.Results.Single()).IsOfUnionType<MediaMatchResult, UnmatchedMedia>();
		await Assert.That(unmatched!.Reason).IsEqualTo(UnmatchedReason.Conflict);
	}

	[Test]
	public async Task SearchAsync_WhenAProviderMissedAService_PassesTheMissOn()
	{
		var media = IMediaService.Mock();
		media.RegisterAsync(Any(), Any(), Any()).Returns([new None()]);
		var repository = IMediaRepository.Mock();
		repository.GetByExternalsAsync(Any(), Any()).Returns(Array.Empty<StoredMedia>());
		var miss = new SearchMiss(RemoteService.Sonarr, "connection refused");

		var results = await Testee(media, repository, [miss]).SearchAsync("Annie", [MediaType.Movie]);

		await Assert.That(results.Missed).IsEquivalentTo([miss]);
	}

	private static MediaSearchService Testee(Mock<IMediaService> media, Mock<IMediaRepository> repository) => Testee(media, repository, []);

	private static MediaSearchService Testee(Mock<IMediaService> media, Mock<IMediaRepository> repository, IReadOnlyList<SearchMiss> missed)
	{
		var provider = IMediaSearchProvider.Mock();
		provider.SearchAsync(Any(), Any(), Any()).Returns(new SearchAnswer([new FoundMedia([Plex, Tmdb], new MovieDetails { Title = "Annie" })], missed));
		var services = new ServiceCollection();
		services.AddKeyedSingleton(SearchProvider.Plex, provider.Object);
		return new MediaSearchService(
			new KeyedServiceIndex<SearchProvider, IMediaSearchProvider>(services.BuildServiceProvider()),
			media.Object,
			repository.Object,
			ProviderConfigStubs.PlexWantlist().Object
		);
	}
}
