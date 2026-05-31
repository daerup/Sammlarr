using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
using Reqnroll.Assist;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistManagementSteps(SammlarrScenarioContext sammlarr, LocalMediaContext local)
{
	private IWatchlistService Service => sammlarr.Resolve<IWatchlistService>();
	private IWatchlistOutbox Outbox => sammlarr.Resolve<IWatchlistOutbox>();

	[Given("the following are on the watchlist:")]
	public async Task GivenOnWatchlist(DataTable table)
	{
		var repository = sammlarr.Resolve<IWatchlistRepository>();
		foreach (var row in table.CreateSet<MediaRow>())
		{
			var origin = string.IsNullOrEmpty(row.Origin) ? DB.Data.Watchlist.WatchlistEntryOrigin.Remote : WatchlistOrigins.Parse(row.Origin);
			await repository.AddAsync([local.MediaIds[row.Media!]], WatchlistOrigins.ToProvenance(origin));
		}
	}

	[Given("{string} has a pending {WatchlistOperationKind} in the outbox")]
	public Task GivenPendingOutboxOperation(string key, WatchlistOperationKind kind) => Outbox.EnqueueAsync(kind, [local.MediaIds[key]]);

	[When("I add {string} to the watchlist with {word} origin")]
	public Task WhenAddWithOrigin(string key, string origin) =>
		Service.AddToWatchlistAsync([local.MediaIds[key]], WatchlistOrigins.ToProvenance(WatchlistOrigins.Parse(origin)));

	[When("I add {string} to the watchlist")]
	public Task WhenAdd(string key) => Service.AddToWatchlistAsync([local.MediaIds[key]], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I add nothing to the watchlist")]
	public Task WhenAddNothing() => Service.AddToWatchlistAsync([], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I add to the watchlist:")]
	public Task WhenAddMany(DataTable table) =>
		Service.AddToWatchlistAsync(
			[.. table.CreateSet<MediaRow>().Select(row => local.MediaIds[row.Media!])],
			new WatchlistItemProvenance(new ManualOrigin())
		);

	[When("I remove {string} from the watchlist")]
	public Task WhenRemove(string key) => Service.RemoveFromWatchlistAsync([local.MediaIds[key]]);

	[Then("the watchlist contains {int} entry with {word} origin")]
	public async Task ThenContainsEntry(int count, string origin)
	{
		var entries = await sammlarr.DbContext.Watchlist.AsNoTracking().ToListAsync();
		await Assert.That(entries).Count().IsEqualTo(count);
		await Assert.That(entries[0].Origin).IsEqualTo(WatchlistOrigins.Parse(origin));
	}

	[Then("the watchlist is empty")]
	public async Task ThenWatchlistIsEmpty() => await Assert.That(await sammlarr.DbContext.Watchlist.CountAsync()).IsEqualTo(0);

	[Then(@"the outbox contains {int} operation\(s\)")]
	public async Task ThenOutboxCount(int count) => await Assert.That(await Outbox.GetAsync()).Count().IsEqualTo(count);

	[Then("the outbox has a pending {WatchlistOperationKind} for {string}")]
	public async Task ThenOutboxHasPending(WatchlistOperationKind kind, string key) =>
		await Assert.That(await Outbox.GetAsync()).Contains(new WatchlistOperation(local.MediaIds[key], kind));

	[Then("the outbox is empty")]
	public async Task ThenOutboxEmpty() => await Assert.That(await Outbox.GetAsync()).IsEmpty();
}
