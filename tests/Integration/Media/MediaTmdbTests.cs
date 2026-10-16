using System.Net;
using Contracts.Media;
using Domain.Media;
using Domain.Providers;
using Harness;
using TestInfrastructure;
using static Harness.Ids;

namespace Integration.Tests.Media;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class MediaTmdbTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	[Test]
	public async Task LoadListAsync_ForATitleResolvedThroughTmdb_ReadsItsDetailsOnce()
	{
		host.SinkAddresses(MediaProvider.Tvdb);
		host.HttpStub.RespondWithStatusForUrl($"{TestUrls.TvmazeApi}/lookup/shows", HttpStatusCode.NotFound);
		await host.TmdbKnowsTheExternalIdsOf(new MediaSpec("Breaking Bad", MediaType.Show, [Tmdb("1396"), Tvdb("81189")]));
		var request = new IdentifyRequest([Tmdb("1396").ToExternal(MediaType.Show)], new None());

		var results = await host.Resolve<IMediaService>()
			.LoadListAsync(new[] { request }.ToAsyncEnumerable().AsListTitles(), FieldNeeds.Everything)
			.InListOrderAsync()
			.ToListAsync();

		await Assert.That(results.Single()).IsOfUnionType<MediaMatchResult, MatchedMedia>();
		await Assert.That(host.HttpStub.Requests($"{TestUrls.TmdbApi}/tv/1396")).IsEqualTo(1);
	}
}
