using Contracts.Watchlist;
using Integration.Tests.Assertions;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media.Local;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistContentsSteps(WatchlistContentsScenarioContext ctx, SammlarrScenarioContext sammlarr, LocalMediaContext local)
{
	[When("I get the watchlist items")]
	public async Task WhenGetWatchlistItems()
	{
		var items = await sammlarr.Resolve<IWatchlistService>().GetWatchlistItemsAsync().ToListAsync();
		ctx.Items = items.Select(i => i.Media.ToMatch()).ToList();
	}

	[When("I get the watchlist IDs")]
	public async Task WhenGetWatchlistIds() => ctx.Ids = await sammlarr.Resolve<IWatchlistService>().GetWatchlistIdsAsync();

	[Then("the watchlist items are:")]
	public Task ThenItemsAre(DataTable table) => MediaDetailAssertions.AssertItemsAsync(ctx.Items!, table);

	[Then("the watchlist IDs include:")]
	public Task ThenIdsInclude(DataTable table) => MediaDetailAssertions.AssertIdsIncludeAsync(ctx.Ids!, table, local);
}
