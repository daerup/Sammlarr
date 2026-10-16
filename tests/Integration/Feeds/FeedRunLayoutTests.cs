using Contracts.Feed;
using Domain.Feed;
using Domain.Providers;
using Harness;

namespace Integration.Tests.Feeds;

[ClassDataSource<SammlarrTestHost>(Shared = SharedType.None)]
public class FeedRunLayoutTests(SammlarrTestHost host) : IAsyncDisposable
{
	public async ValueTask DisposeAsync() => await host.DisposeAsync();

	private IFeedRunLayout Testee => host.Resolve<IFeedRunLayout>();

	[Test]
	public async Task GetAsync_ForAFeedIntoPlex_LeadsToThePlexIds()
	{
		await host.EnsurePlexAccount();
		await host.WithTmdbAccount(SammlarrTestHost.TmdbApiKey);
		await host.WithMdblistAccount(SammlarrTestHost.MdblistApiKey);
		var feed = await host.SeedFeedAsync();

		var layout = await Testee.GetAsync(feed);

		await Assert.That(layout.Sink).IsEqualTo(SinkKind.Plex);
		await Assert.That(layout.Targets).IsEquivalentTo([MediaProvider.Plex]);
		await Assert.That(layout.Ids.Select(node => node.Service)).Contains(RemoteService.Plex);
		await Assert.That(layout.Details).Contains(RemoteService.Tmdb);
	}

	[Test]
	public async Task GetAsync_ForAFeedThatIsGone_LaysOutNothing()
	{
		var layout = await Testee.GetAsync(FeedId.From(Guid.NewGuid()));

		await Assert.That(layout.Ids).IsEmpty();
		await Assert.That(layout.Edges).IsEmpty();
		await Assert.That(layout.Sink).IsEqualTo(SinkKind.Plex);
	}
}
