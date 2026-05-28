using Contracts.Auth;
using Contracts.Media;
using Contracts.Media.Metadata;
using Contracts.Media.Search;
using Contracts.Media.Trending;
using Contracts.Settings;
using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Media;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistOutboxSteps(CachedMediaContext cachedMedia, SammlarrScenarioContext sammlarr)
{
	[Given("{string} is on the watchlist")]
	public async Task GivenOnWatchlist(string title) =>
		await sammlarr.Resolve<IWatchlistRepository>().AddAsync([cachedMedia.MediaIds[title]], new WatchlistItemProvenance(new RemoteOrigin()));

	[Given("{string} has a pending {WatchlistOperationKind} in the outbox")]
	public async Task GivenPendingOutboxOperation(string title, WatchlistOperationKind kind) =>
		await sammlarr.Resolve<IWatchlistOutbox>().EnqueueAsync(kind, [cachedMedia.MediaIds[title]]);

	[When("I add {string} to the watchlist")]
	public async Task WhenAdd(string title) =>
		await sammlarr.Resolve<IWatchlistService>().AddToWatchlistAsync([cachedMedia.MediaIds[title]], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I remove {string} from the watchlist")]
	public async Task WhenRemove(string title) => await sammlarr.Resolve<IWatchlistService>().RemoveFromWatchlistAsync([cachedMedia.MediaIds[title]]);

	[Then(@"the outbox contains {int} operation\(s\)")]
	public async Task ThenOutboxCount(int count)
	{
		var ops = await sammlarr.Resolve<IWatchlistOutbox>().GetAsync();
		await Assert.That(ops).Count().IsEqualTo(count);
	}

	[Then("the outbox has a pending {WatchlistOperationKind} for {string}")]
	public async Task ThenOutboxHasPending(WatchlistOperationKind kind, string title)
	{
		var id = cachedMedia.MediaIds[title];
		var ops = await sammlarr.Resolve<IWatchlistOutbox>().GetAsync();
		await Assert.That(ops).Contains(new WatchlistOperation(id, kind));
	}

	[When("I add nothing to the watchlist")]
	public async Task WhenAddNothing() => await sammlarr.Resolve<IWatchlistService>().AddToWatchlistAsync([], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I add {string}, {string}, and {string} to the watchlist")]
	public async Task WhenAddMultiple(string key1, string key2, string key3) =>
		await sammlarr
			.Resolve<IWatchlistService>()
			.AddToWatchlistAsync(
				[cachedMedia.MediaIds[key1], cachedMedia.MediaIds[key2], cachedMedia.MediaIds[key3]],
				new WatchlistItemProvenance(new ManualOrigin())
			);

	[Then("the outbox is empty")]
	public async Task ThenOutboxEmpty()
	{
		var ops = await sammlarr.Resolve<IWatchlistOutbox>().GetAsync();
		await Assert.That(ops).IsEmpty();
	}
}
