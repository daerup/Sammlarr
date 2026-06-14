using Contracts.Watchlist;
using Contracts.Watchlist.Outbox;
using Integration.Tests.Infrastructure.Common;
using Integration.Tests.Infrastructure.Hosting;
using Integration.Tests.Infrastructure.Media;
using Integration.Tests.Infrastructure.Media.Local;
using Microsoft.EntityFrameworkCore;
using Reqnroll.Assist;

namespace Integration.Tests.Watchlist;

[Binding]
public class WatchlistManagementSteps(SammlarrScenarioContext sammlarr, LocalMediaContext local, ExceptionContext exceptionContext)
{
	private IWatchlistService Service => sammlarr.Resolve<IWatchlistService>();
	private IWatchlistOutbox Outbox => sammlarr.Resolve<IWatchlistOutbox>();

	[Given("the local watchlist has:")]
	public async Task GivenOnWatchlist(DataTable table)
	{
		var repository = sammlarr.Resolve<IWatchlistRepository>();
		foreach (var row in table.CreateSet<HandleRow>())
		{
			var origin = string.IsNullOrEmpty(row.Origin) ? DB.Data.Watchlist.WatchlistEntryOrigin.Remote : WatchlistOrigins.Parse(row.Origin);
			await repository.AddAsync([local.MediaIds[row.Media!]], WatchlistOrigins.ToProvenance(origin));
		}
	}

	[When("I add {string} to the watchlist with {word} origin")]
	public Task WhenAddWithOrigin(string key, string origin) =>
		Service.AddToWatchlistAsync([local.MediaIds[key]], WatchlistOrigins.ToProvenance(WatchlistOrigins.Parse(origin)));

	[When("I add {string} to the watchlist")]
	public Task WhenAdd(string key) => Service.AddToWatchlistAsync([local.MediaIds[key]], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I add nothing to the watchlist")]
	public Task WhenAddNothing() => Service.AddToWatchlistAsync([], new WatchlistItemProvenance(new ManualOrigin()));

	[When("I try to add {string} to the watchlist")]
	public Task WhenTryAdd(string key) =>
		exceptionContext.TryExecuteAsync(() => Service.AddToWatchlistAsync([local.MediaIds[key]], new WatchlistItemProvenance(new ManualOrigin())));

	[When("I try to remove {string} from the watchlist")]
	public Task WhenTryRemove(string key) => exceptionContext.TryExecuteAsync(() => Service.RemoveFromWatchlistAsync([local.MediaIds[key]]));

	[When("I remove nothing from the watchlist")]
	public Task WhenRemoveNothing() => Service.RemoveFromWatchlistAsync([]);

	[When("I add to the watchlist:")]
	public Task WhenAddMany(DataTable table) =>
		Service.AddToWatchlistAsync([.. table.CreateSet<HandleRow>().Select(row => local.MediaIds[row.Media!])], new WatchlistItemProvenance(new ManualOrigin()));

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

	[Then("the outbox is empty")]
	public async Task ThenOutboxEmpty() => await Assert.That(await Outbox.GetAsync()).IsEmpty();

	[Given("the outbox has:")]
	public async Task GivenOutboxHas(DataTable table)
	{
		foreach (var row in table.CreateSet<OutboxRow>())
			await Outbox.EnqueueAsync(Enum.Parse<WatchlistOperationKind>(row.Operation!, ignoreCase: true), [local.MediaIds[row.Key!]]);
	}

	[Then("the outbox is:")]
	public async Task ThenOutboxIs(DataTable table)
	{
		var expected = table.CreateSet<OutboxRow>().ToList();
		var actual = await Outbox.GetAsync();
		await Assert.That(actual).Count().IsEqualTo(expected.Count);
		foreach (var row in expected)
		{
			var mediaId = local.MediaIds[row.Key!];
			var kind = Enum.Parse<WatchlistOperationKind>(row.Operation!, ignoreCase: true);
			await Assert.That(actual).Contains(new WatchlistOperation(mediaId, kind));
			if (row.Attempts is { } attempts)
			{
				sammlarr.DbContext.ChangeTracker.Clear();
				var entry = await sammlarr.DbContext.WatchlistOutbox.AsNoTracking().FirstAsync(o => o.Media.Id == mediaId.Value);
				await Assert.That(entry.Attempts).IsEqualTo(attempts);
			}
		}
	}
}
