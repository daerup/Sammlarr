using Contracts.Watchlist;
using Integration.Tests.Infrastructure;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistItemsSteps(WatchlistItemsScenarioContext ctx, MediaDetailContext detailCtx, SammlarrScenarioContext sammlarr)
{
	[When("I get the watchlist items")]
	public async Task WhenGetWatchlistItems()
	{
		ctx.Items = await sammlarr.Resolve<IWatchlistService>().GetWatchlistItemsAsync().ToListAsync();
		detailCtx.Items = ctx.Items.Select(i => i.Detail).ToList();
	}

	[When("I get the watchlist IDs")]
	public async Task WhenGetWatchlistIds()
	{
		ctx.Ids = await sammlarr.Resolve<IWatchlistService>().GetWatchlistIdsAsync();
		detailCtx.Ids = ctx.Ids;
	}
}
